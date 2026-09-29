using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Inventory;
using Restaurant.Domain.Payment;
using Restaurant.Domain.Sales;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Api.Controllers;

/// <summary>
/// P0 reporting scope per PRD §17/§10.12: daily sales and stock levels, Manager view
/// only. Both are read-only aggregates over data already written by other
/// controllers — no new state, no new invariants to protect.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Owner,Manager")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Revenue is Confirmed-Payment based (same source as Shift Close's
    /// reconciliation), not Order.TotalAmount — that keeps this consistent with
    /// what Shift Close reports, and correctly handles the (currently theoretical,
    /// since Checkout takes full payment) case of a partially-paid order. Filtering
    /// on Order.Status == Completed / Voided (not the Payment row) is what excludes
    /// a voided sale's cash from CashTotal — Void never edits/deletes the original
    /// Payment (see OrdersController.Void, Shift Close notes).
    /// </summary>
    [HttpGet("sales-daily")]
    public async Task<ActionResult<SalesDailyReportResponse>> SalesDaily([FromQuery] DateOnly? date)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var rangeStart = new DateTimeOffset(targetDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeEnd = rangeStart.AddDays(1);

        var ordersInRange = _db.Orders.Where(o => o.CreatedAt >= rangeStart && o.CreatedAt < rangeEnd);

        var completedOrderCount = await ordersInRange.CountAsync(o => o.Status == OrderStatus.Completed);
        var voidedOrderCount = await ordersInRange.CountAsync(o => o.Status == OrderStatus.Voided);

        var confirmedPayments = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Confirmed)
            .Join(ordersInRange.Where(o => o.Status == OrderStatus.Completed),
                p => p.OrderId, o => o.Id, (p, o) => p)
            .Join(_db.PaymentMethods, p => p.PaymentMethodId, m => m.Id, (p, m) => new { p.Amount, m.Code })
            .ToListAsync();

        var cashTotal = confirmedPayments.Where(x => x.Code == PaymentMethodCode.Cash).Sum(x => x.Amount);
        var nonCashTotal = confirmedPayments.Where(x => x.Code != PaymentMethodCode.Cash).Sum(x => x.Amount);

        return Ok(new SalesDailyReportResponse(
            targetDate,
            completedOrderCount,
            voidedOrderCount,
            cashTotal,
            nonCashTotal,
            cashTotal + nonCashTotal));
    }

    /// <summary>
    /// IsBelowMinimum/BelowMinimumCount are not in the original PRD — added by
    /// explicit agreement as the data foundation for a low-stock signal. A
    /// MinimumStock of exactly 0 means "no threshold configured" (see Ingredient.cs),
    /// so it's never flagged — otherwise every never-configured ingredient would show
    /// as perpetually below minimum. This is report-only; there's no active
    /// notification (WhatsApp/email/push) wired to it, that's a deliberately deferred,
    /// separate decision pending a channel/vendor choice.
    /// </summary>
    [HttpGet("stock-levels")]
    public async Task<ActionResult<StockLevelReportResponse>> StockLevels()
    {
        var items = await _db.Ingredients
            .OrderBy(i => i.Name)
            .GroupJoin(_db.Stocks, i => i.Id, s => s.IngredientId, (i, stocks) => new { i, stocks })
            .SelectMany(x => x.stocks.DefaultIfEmpty(), (x, stock) => new
            {
                x.i.Id,
                x.i.Name,
                x.i.Unit,
                x.i.MinimumStock,
                CurrentStock = stock == null ? 0 : stock.Quantity
            })
            .Select(x => new StockLevelReportItem(
                x.Id, x.Name, x.Unit, x.CurrentStock, x.MinimumStock,
                x.MinimumStock > 0 && x.CurrentStock < x.MinimumStock))
            .ToListAsync();

        return Ok(new StockLevelReportResponse(DateTimeOffset.UtcNow, items.Count(i => i.IsBelowMinimum), items));
    }

    /// <summary>
    /// Not in the original PRD — added by explicit agreement to close the loop on
    /// the Waste category (StockMovementReason.Waste, added alongside the
    /// Purchasing module): the category existed so waste could "be reported on
    /// separately," but nothing actually read it into a value until this endpoint.
    /// Defaults to the current month rather than a single day (unlike sales-daily)
    /// because "how much did we lose to waste" is naturally asked as a monthly
    /// question, not a daily one. WasteValue uses StockMovement.UnitCostAtTime — the
    /// Ingredient's Weighted-Average cost *snapshotted at the moment the waste was
    /// recorded* (IngredientsController.AdjustStock), not today's current cost, so
    /// this report doesn't silently drift if AverageCost changes after the fact.
    /// Movements recorded before this field existed, or against an Ingredient that
    /// had no AverageCost yet, show a 0 value for that entry — a known reporting
    /// gap, not a crash.
    /// </summary>
    [HttpGet("waste")]
    public async Task<ActionResult<WasteReportResponse>> Waste([FromQuery] int? year, [FromQuery] int? month)
    {
        var now = DateTime.UtcNow;
        var targetYear = year ?? now.Year;
        var targetMonth = month ?? now.Month;

        var rangeStart = new DateTimeOffset(new DateTime(targetYear, targetMonth, 1), TimeSpan.Zero);
        var rangeEnd = rangeStart.AddMonths(1);

        var wasteMovements = await _db.StockMovements
            .Where(m => m.Reason == StockMovementReason.Waste && m.CreatedAt >= rangeStart && m.CreatedAt < rangeEnd)
            .Join(_db.Ingredients, m => m.IngredientId, i => i.Id, (m, i) => new { m.ChangeQuantity, m.UnitCostAtTime, i.Id, i.Name, i.Unit })
            .ToListAsync();

        var items = wasteMovements
            .GroupBy(x => new { x.Id, x.Name, x.Unit })
            .Select(g => new WasteReportItem(
                g.Key.Id,
                g.Key.Name,
                g.Key.Unit,
                -g.Sum(x => x.ChangeQuantity), // ChangeQuantity is negative for Waste; report a positive "quantity lost"
                -g.Sum(x => x.ChangeQuantity * (x.UnitCostAtTime ?? 0m))))
            .OrderByDescending(i => i.WasteValue)
            .ToList();

        return Ok(new WasteReportResponse(targetYear, targetMonth, items.Sum(i => i.WasteValue), items));
    }
}
