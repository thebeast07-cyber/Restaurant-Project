using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Domain.Audit;
using Restaurant.Domain.Common;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Catalog;
using Restaurant.Domain.Inventory;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

[ApiController]
[Route("api/ingredients")]
[Authorize]
public class IngredientsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public IngredientsController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<IngredientResponse>>> List()
    {
        var ingredients = await _db.Ingredients
            .OrderBy(i => i.Name)
            .GroupJoin(_db.Stocks, i => i.Id, s => s.IngredientId, (i, stocks) => new { i, stocks })
            .SelectMany(x => x.stocks.DefaultIfEmpty(), (x, stock) =>
                new IngredientResponse(x.i.Id, x.i.Name, x.i.Unit, stock == null ? 0 : stock.Quantity, x.i.MinimumStock))
            .ToListAsync();

        return Ok(ingredients);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<IngredientResponse>> Create(CreateIngredientRequest request)
    {
        var ingredient = new Ingredient
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            Name = request.Name,
            Unit = request.Unit,
            MinimumStock = request.MinimumStock
        };

        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync();

        return Ok(new IngredientResponse(ingredient.Id, ingredient.Name, ingredient.Unit, 0, ingredient.MinimumStock));
    }

    /// <summary>
    /// Not in the original PRD — added by explicit agreement so a low-stock threshold
    /// can be set for ingredients that already exist (seeded/created before this was
    /// added), not just new ones via Create's optional field. This is the data
    /// foundation for a low-stock signal; there's no active notification (WhatsApp/
    /// email/push) wired to it yet — that's a separate, deliberately deferred decision
    /// pending a channel/vendor choice, same shape as the payment-gateway question.
    /// </summary>
    [HttpPut("{ingredientId:guid}/minimum-stock")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> UpdateMinimumStock(Guid ingredientId, UpdateMinimumStockRequest request)
    {
        var updated = await _db.Ingredients
            .Where(i => i.Id == ingredientId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.MinimumStock, request.MinimumStock));

        return updated == 0 ? NotFound() : NoContent();
    }

    /// <summary>
    /// The StockAdjusted event (Day 10): warehouse-side correction of Stock.Quantity,
    /// either a physical count (Opname — CountedQuantity is the new absolute value)
    /// or a known delta (ManualAdjustment — e.g. spoilage, receiving miscount).
    /// Restricted to Owner/Manager because there's no separate Warehouse role in the
    /// MVP's fixed 3-role enum (see implementation notes, Identity & Auth) — every
    /// permission the discovery docs assign to "Warehouse Staff" is exercised by
    /// Owner/Manager here instead. Writes an AuditLog with before/after quantity,
    /// same requirement as Void (PRD §12: sensitive actions need who/when/what).
    ///
    /// Both branches apply Quantity via an atomic conditional UPDATE, never a
    /// read-then-write on the tracked entity — same discipline as Checkout's stock
    /// deduction (Day 5) and Void's stock restore (Day 9), because a warehouse opname
    /// racing a concurrent Sale/ManualAdjustment on the same Ingredient is exactly the
    /// lost-update shape Day 5 found, just triggered by a different actor.
    /// </summary>
    [HttpPost("{ingredientId:guid}/stock-adjustment")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<StockAdjustmentResponse>> AdjustStock(Guid ingredientId, StockAdjustmentRequest request)
    {
        if (request.CountedQuantity is null == request.DeltaQuantity is null)
        {
            return BadRequest(new { message = "Provide exactly one of CountedQuantity (Opname) or DeltaQuantity (ManualAdjustment)." });
        }

        if (!await _db.Ingredients.AnyAsync(i => i.Id == ingredientId))
        {
            return NotFound();
        }

        var userId = User.GetUserId();

        // Upsert-by-catch: two concurrent first-ever adjustments on a brand-new
        // Ingredient can both try to create the Stock row; the unique index on
        // (TenantId, BranchId, IngredientId) lets one through, same race-handling
        // shape as ShiftsController.Open's partial unique index.
        if (!await _db.Stocks.AnyAsync(s => s.IngredientId == ingredientId))
        {
            _db.Stocks.Add(new Stock
            {
                TenantId = _tenant.TenantId!.Value,
                BranchId = _tenant.BranchId!.Value,
                IngredientId = ingredientId,
                Quantity = 0
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
            }
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        decimal quantityBefore;
        decimal quantityAfter;
        decimal changeQuantity;
        StockMovementReason reason;

        if (request.DeltaQuantity is not null)
        {
            reason = request.DeltaReason ?? StockMovementReason.ManualAdjustment;
            if (reason is not (StockMovementReason.ManualAdjustment or StockMovementReason.Waste))
            {
                return BadRequest(new { message = "DeltaReason must be ManualAdjustment or Waste when DeltaQuantity is given." });
            }

            changeQuantity = request.DeltaQuantity.Value;

            if (changeQuantity == 0)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = "Adjustment results in no change to stock." });
            }

            // A pure delta needs no compare-and-swap — applying it as a conditional
            // increment is correct no matter what Quantity currently is.
            var rows = await _db.Stocks
                .Where(s => s.IngredientId == ingredientId && s.Quantity + changeQuantity >= 0)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Quantity, s => s.Quantity + changeQuantity));

            if (rows == 0)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = "Adjustment would result in negative stock." });
            }

            quantityAfter = await _db.Stocks.Where(s => s.IngredientId == ingredientId)
                .Select(s => s.Quantity).SingleAsync();
            quantityBefore = quantityAfter - changeQuantity;
        }
        else
        {
            reason = StockMovementReason.Opname;

            // Opname sets an absolute counted value, so it can only be applied
            // compare-and-swap style against the exact Quantity it was counted
            // against — if something else changes Quantity between our read and
            // write, the stale "the count was X" write must not silently clobber it.
            quantityBefore = await _db.Stocks.Where(s => s.IngredientId == ingredientId)
                .Select(s => s.Quantity).SingleAsync();
            changeQuantity = request.CountedQuantity!.Value - quantityBefore;

            if (changeQuantity == 0)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = "Adjustment results in no change to stock." });
            }

            var rows = await _db.Stocks
                .Where(s => s.IngredientId == ingredientId && s.Quantity == quantityBefore)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Quantity, request.CountedQuantity!.Value));

            if (rows == 0)
            {
                await transaction.RollbackAsync();
                return Conflict(new { message = "Stock changed concurrently while counting — retry the opname." });
            }

            quantityAfter = request.CountedQuantity.Value;
        }

        _db.StockMovements.Add(new StockMovement
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            IngredientId = ingredientId,
            ChangeQuantity = changeQuantity,
            Reason = reason,
            ReferenceType = "Ingredient",
            ReferenceId = ingredientId,
            CreatedByUserId = userId
        });

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            UserId = userId,
            Action = AuditAction.StockAdjustment,
            EntityType = "Ingredient",
            EntityId = ingredientId,
            BeforeValue = JsonSerializer.Serialize(new { Quantity = quantityBefore }),
            AfterValue = JsonSerializer.Serialize(new { Quantity = quantityAfter }),
            Reason = request.Reason
        });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new StockAdjustmentResponse(ingredientId, quantityBefore, quantityAfter, changeQuantity, reason.ToString()));
    }
}
