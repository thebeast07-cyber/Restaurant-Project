using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Auth;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Audit;
using Restaurant.Domain.Common;
using Restaurant.Domain.Inventory;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

/// <summary>
/// Batches multiple per-Ingredient Opname counts into one named counting event. The
/// underlying mechanism is unchanged from IngredientsController.AdjustStock's
/// CountedQuantity path (same compare-and-swap-against-the-counted-value safety, same
/// StockMovement/AuditLog shape) — this only adds a StockOpnameSession header and
/// processes many Ingredients in one all-or-nothing transaction instead of one
/// endpoint call per Ingredient.
/// </summary>
[ApiController]
[Route("api/stock-opname")]
[Authorize(Roles = "Owner,Manager")]
public class StockOpnameController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantProvider _tenant;

    public StockOpnameController(AppDbContext db, ICurrentTenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<StockOpnameSessionResponse>>> List()
    {
        var sessions = await _db.StockOpnameSessions.OrderByDescending(s => s.Date).ToListAsync();

        var response = new List<StockOpnameSessionResponse>();
        foreach (var session in sessions)
        {
            response.Add(await BuildResponseAsync(session));
        }
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockOpnameSessionResponse>> Get(Guid id)
    {
        var session = await _db.StockOpnameSessions.SingleOrDefaultAsync(s => s.Id == id);
        if (session is null)
        {
            return NotFound();
        }
        return Ok(await BuildResponseAsync(session));
    }

    /// <summary>
    /// Lines whose CountedQuantity matches the current system Quantity are silently
    /// skipped (no StockMovement) rather than rejected — the counting screen
    /// prefills every Ingredient's current quantity, so most lines in a real session
    /// are untouched by the staff doing the count, not genuine zero-change corrections
    /// to reject. Everything else (missing Ingredient, duplicate IngredientId,
    /// negative count, a concurrent Quantity change mid-session) fails the whole
    /// session atomically — same all-or-nothing guarantee as Checkout, so a partially
    /// applied opname (some Ingredients corrected, others not) can never happen.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StockOpnameSessionResponse>> Create(CreateStockOpnameSessionRequest request)
    {
        if (request.Lines.Count == 0)
        {
            return BadRequest(new { message = "A Stock Opname session needs at least one counted Ingredient." });
        }
        if (request.Lines.Any(l => l.CountedQuantity < 0))
        {
            return BadRequest(new { message = "CountedQuantity cannot be negative." });
        }
        if (request.Lines.Select(l => l.IngredientId).Distinct().Count() != request.Lines.Count)
        {
            return BadRequest(new { message = "Duplicate IngredientId in Lines." });
        }

        var ingredientIds = request.Lines.Select(l => l.IngredientId).ToList();
        var validIngredientCount = await _db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id));
        if (validIngredientCount != ingredientIds.Count)
        {
            return BadRequest(new { message = "One or more IngredientId values are invalid." });
        }

        var userId = User.GetUserId();

        // Upsert-by-catch for any Ingredient that has no Stock row yet — same pattern
        // as IngredientsController.AdjustStock and PurchasesController.Create.
        var missingStockIngredientIds = ingredientIds.Except(
            await _db.Stocks.Where(s => ingredientIds.Contains(s.IngredientId)).Select(s => s.IngredientId).ToListAsync()
        ).ToList();

        if (missingStockIngredientIds.Count > 0)
        {
            foreach (var ingredientId in missingStockIngredientIds)
            {
                _db.Stocks.Add(new Stock
                {
                    TenantId = _tenant.TenantId!.Value,
                    BranchId = _tenant.BranchId!.Value,
                    IngredientId = ingredientId,
                    Quantity = 0
                });
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
            }
        }

        var session = new StockOpnameSession
        {
            TenantId = _tenant.TenantId!.Value,
            BranchId = _tenant.BranchId!.Value,
            Date = request.Date,
            Notes = request.Notes,
            CreatedByUserId = userId
        };

        await using var transaction = await _db.Database.BeginTransactionAsync();

        foreach (var line in request.Lines)
        {
            var quantityBefore = await _db.Stocks.Where(s => s.IngredientId == line.IngredientId)
                .Select(s => s.Quantity).SingleAsync();
            var changeQuantity = line.CountedQuantity - quantityBefore;

            if (changeQuantity == 0)
            {
                continue;
            }

            var rows = await _db.Stocks
                .Where(s => s.IngredientId == line.IngredientId && s.Quantity == quantityBefore)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Quantity, line.CountedQuantity));

            if (rows == 0)
            {
                await transaction.RollbackAsync();
                return Conflict(new { message = $"Stock changed concurrently for IngredientId {line.IngredientId} while counting — retry the opname." });
            }

            _db.StockMovements.Add(new StockMovement
            {
                TenantId = _tenant.TenantId!.Value,
                BranchId = _tenant.BranchId!.Value,
                IngredientId = line.IngredientId,
                ChangeQuantity = changeQuantity,
                Reason = StockMovementReason.Opname,
                ReferenceType = "Ingredient",
                ReferenceId = line.IngredientId,
                CreatedByUserId = userId,
                OpnameSessionId = session.Id,
                QuantityBefore = quantityBefore
                // UnitCostAtTime left null — Opname is a count correction, not a
                // valued loss/gain, same reasoning as the single-ingredient endpoint.
            });

            _db.AuditLogs.Add(new AuditLog
            {
                TenantId = _tenant.TenantId!.Value,
                BranchId = _tenant.BranchId!.Value,
                UserId = userId,
                Action = AuditAction.StockAdjustment,
                EntityType = "Ingredient",
                EntityId = line.IngredientId,
                BeforeValue = JsonSerializer.Serialize(new { Quantity = quantityBefore }),
                AfterValue = JsonSerializer.Serialize(new { Quantity = line.CountedQuantity }),
                Reason = $"Stock Opname session {session.Id}"
            });
        }

        _db.StockOpnameSessions.Add(session);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(await BuildResponseAsync(session));
    }

    private async Task<StockOpnameSessionResponse> BuildResponseAsync(StockOpnameSession session)
    {
        var movements = await _db.StockMovements
            .Where(m => m.OpnameSessionId == session.Id)
            .ToListAsync();

        var ingredientIds = movements.Select(m => m.IngredientId).ToList();
        var ingredients = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i);

        var lines = movements.Select(m =>
        {
            var ingredient = ingredients.GetValueOrDefault(m.IngredientId);
            var quantityBefore = m.QuantityBefore!.Value;
            return new StockOpnameLineResponse(
                m.IngredientId,
                ingredient?.Name ?? "?",
                ingredient?.Unit ?? "?",
                QuantityBefore: quantityBefore,
                QuantityAfter: quantityBefore + m.ChangeQuantity,
                ChangeQuantity: m.ChangeQuantity);
        }).ToList();

        return new StockOpnameSessionResponse(session.Id, session.Date, session.Notes, session.CreatedByUserId, lines);
    }
}
