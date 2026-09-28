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
                new IngredientResponse(x.i.Id, x.i.Name, x.i.Unit, stock == null ? 0 : stock.Quantity))
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
            Unit = request.Unit
        };

        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync();

        return Ok(new IngredientResponse(ingredient.Id, ingredient.Name, ingredient.Unit, 0));
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
    /// </summary>
    [HttpPost("{ingredientId:guid}/stock-adjustment")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<StockAdjustmentResponse>> AdjustStock(Guid ingredientId, StockAdjustmentRequest request)
    {
        if (request.CountedQuantity is null == request.DeltaQuantity is null)
        {
            return BadRequest(new { message = "Provide exactly one of CountedQuantity (Opname) or DeltaQuantity (ManualAdjustment)." });
        }

        var ingredient = await _db.Ingredients.SingleOrDefaultAsync(i => i.Id == ingredientId);
        if (ingredient is null)
        {
            return NotFound();
        }

        var userId = User.GetUserId();

        // Not a hot concurrent path like Checkout (one warehouse staffer counting
        // stock at a time in practice) — read-then-write here is an accepted
        // trade-off, unlike the ExecuteUpdateAsync pattern used for Sale/Void.
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var stock = await _db.Stocks.SingleOrDefaultAsync(s => s.IngredientId == ingredientId);
        if (stock is null)
        {
            stock = new Stock
            {
                TenantId = _tenant.TenantId!.Value,
                BranchId = _tenant.BranchId!.Value,
                IngredientId = ingredientId,
                Quantity = 0
            };
            _db.Stocks.Add(stock);
        }

        var quantityBefore = stock.Quantity;
        StockMovementReason reason;
        decimal changeQuantity;

        if (request.CountedQuantity is not null)
        {
            reason = StockMovementReason.Opname;
            changeQuantity = request.CountedQuantity.Value - quantityBefore;
        }
        else
        {
            reason = StockMovementReason.ManualAdjustment;
            changeQuantity = request.DeltaQuantity!.Value;
        }

        var quantityAfter = quantityBefore + changeQuantity;
        if (quantityAfter < 0)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = "Adjustment would result in negative stock." });
        }

        if (changeQuantity == 0)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = "Adjustment results in no change to stock." });
        }

        stock.Quantity = quantityAfter;

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
