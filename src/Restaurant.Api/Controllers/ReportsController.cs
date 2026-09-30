using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Api.Contracts;
using Restaurant.Domain.Finance;
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

    /// <summary>
    /// Phase 7 (charts/comparison): same revenue logic as <see cref="SalesDaily"/>,
    /// just grouped by day across a range instead of one query per day — the
    /// frontend calls this once for a trend chart, and twice (two ranges) for
    /// period-over-period comparison, diffing client-side. No new state, still a
    /// read-only aggregate.
    /// </summary>
    [HttpGet("sales-range")]
    public async Task<ActionResult<SalesRangeResponse>> SalesRange([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from)
        {
            return BadRequest(new { message = "'to' must not be before 'from'." });
        }

        var rangeStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeEndExclusive = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);

        var ordersInRange = await _db.Orders
            .Where(o => o.CreatedAt >= rangeStart && o.CreatedAt < rangeEndExclusive
                && (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Voided))
            .Select(o => new { o.Id, o.CreatedAt, o.Status })
            .ToListAsync();

        var completedOrderIds = ordersInRange.Where(o => o.Status == OrderStatus.Completed).Select(o => o.Id).ToList();

        var confirmedPayments = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Confirmed && completedOrderIds.Contains(p.OrderId))
            .Join(_db.PaymentMethods, p => p.PaymentMethodId, m => m.Id, (p, m) => new { p.OrderId, p.Amount, m.Code })
            .ToListAsync();

        var ordersById = ordersInRange.ToDictionary(o => o.Id);

        var days = new List<SalesDailyReportResponse>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var dayOrders = ordersInRange.Where(o => DateOnly.FromDateTime(o.CreatedAt.UtcDateTime) == date).ToList();
            var completedCount = dayOrders.Count(o => o.Status == OrderStatus.Completed);
            var voidedCount = dayOrders.Count(o => o.Status == OrderStatus.Voided);

            var dayPayments = confirmedPayments.Where(p => ordersById.TryGetValue(p.OrderId, out var o)
                && DateOnly.FromDateTime(o.CreatedAt.UtcDateTime) == date).ToList();
            var cashTotal = dayPayments.Where(p => p.Code == PaymentMethodCode.Cash).Sum(p => p.Amount);
            var nonCashTotal = dayPayments.Where(p => p.Code != PaymentMethodCode.Cash).Sum(p => p.Amount);

            days.Add(new SalesDailyReportResponse(date, completedCount, voidedCount, cashTotal, nonCashTotal, cashTotal + nonCashTotal));
        }

        return Ok(new SalesRangeResponse(from, to, days));
    }

    /// <summary>
    /// Phase 7: same aggregation as <see cref="Waste"/>, grouped by month across a
    /// range instead of one call per month.
    /// </summary>
    [HttpGet("waste-range")]
    public async Task<ActionResult<WasteRangeResponse>> WasteRange([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from)
        {
            return BadRequest(new { message = "'to' must not be before 'from'." });
        }

        var rangeStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeEndExclusive = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);

        var wasteMovements = await _db.StockMovements
            .Where(m => m.Reason == StockMovementReason.Waste && m.CreatedAt >= rangeStart && m.CreatedAt < rangeEndExclusive)
            .Select(m => new { m.ChangeQuantity, m.UnitCostAtTime, m.CreatedAt })
            .ToListAsync();

        var months = new List<WasteRangeItem>();
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            var monthTotal = wasteMovements
                .Where(m => m.CreatedAt.Year == month.Year && m.CreatedAt.Month == month.Month)
                .Sum(m => -m.ChangeQuantity * (m.UnitCostAtTime ?? 0m));
            months.Add(new WasteRangeItem(month.Year, month.Month, monthTotal));
        }

        return Ok(new WasteRangeResponse(from, to, months));
    }

    /// <summary>
    /// Phase 7: reconstructs a daily stock-balance series per Ingredient from the
    /// StockMovement ledger — there's no separate stock-history table, so "what was
    /// the balance on day X" is derived by walking the ledger, not read directly.
    ///
    /// Math: currentStock (Stock.Quantity, always "now") minus the sum of every
    /// movement that happened strictly after `from`'s start gives the balance
    /// exactly as it stood at the start of `from` — every later movement is what
    /// turned that starting balance into today's currentStock, so subtracting all of
    /// them "rewinds" it. This requires movements all the way up to *now*, not just
    /// up to `to` — a movement between `to` and today still happened "after `from`"
    /// and must be rewound too, even though it falls outside the visible range.
    /// From that starting balance, the series for [from, to] is then walked forward
    /// day by day using only the movements that actually fall inside the range.
    ///
    /// NetChangePerDay is (endBalance - startBalance) / days — the net trend over the
    /// range, not gross consumption. A Purchase and a Sale on the same ingredient
    /// partially offset in this number by design: what matters operationally is
    /// whether the ingredient is trending down overall, not a breakdown of why.
    /// BelowMinimumDays only counts days where MinimumStock > 0 (an unconfigured
    /// threshold, same "0 means not configured" rule as every other report here).
    /// </summary>
    [HttpGet("stock-trend")]
    public async Task<ActionResult<StockTrendResponse>> StockTrend([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from)
        {
            return BadRequest(new { message = "'to' must not be before 'from'." });
        }

        var rangeStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeEndExclusive = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);

        var ingredients = await _db.Ingredients
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.Unit, i.MinimumStock })
            .ToListAsync();

        var currentStocks = await _db.Stocks
            .ToDictionaryAsync(s => s.IngredientId, s => s.Quantity);

        var movementsSinceFrom = await _db.StockMovements
            .Where(m => m.CreatedAt >= rangeStart)
            .Select(m => new { m.IngredientId, m.ChangeQuantity, m.CreatedAt })
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var items = new List<StockTrendItem>();
        foreach (var ingredient in ingredients)
        {
            var currentStock = currentStocks.GetValueOrDefault(ingredient.Id, 0m);
            var ingredientMovements = movementsSinceFrom.Where(m => m.IngredientId == ingredient.Id).ToList();

            var startBalance = currentStock - ingredientMovements.Sum(m => m.ChangeQuantity);

            var series = new List<StockTrendPoint>();
            var runningBalance = startBalance;
            var belowMinimumDays = 0;
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var dayChange = ingredientMovements
                    .Where(m => DateOnly.FromDateTime(m.CreatedAt.UtcDateTime) == date)
                    .Sum(m => m.ChangeQuantity);
                runningBalance += dayChange;
                series.Add(new StockTrendPoint(date, runningBalance));
                if (ingredient.MinimumStock > 0 && runningBalance < ingredient.MinimumStock)
                {
                    belowMinimumDays++;
                }
            }

            var numberOfDays = to.DayNumber - from.DayNumber + 1;
            var netChangePerDay = numberOfDays > 0 ? (runningBalance - startBalance) / numberOfDays : 0m;

            items.Add(new StockTrendItem(
                ingredient.Id, ingredient.Name, ingredient.Unit, ingredient.MinimumStock,
                belowMinimumDays, netChangePerDay, series));
        }

        return Ok(new StockTrendResponse(from, to, items));
    }

    /// <summary>
    /// Multi-step income statement (Revenue → COGS → Gross Profit → Operating
    /// Expenses → Net Profit) — standard format, not an invented one (checked
    /// against the Majoo benchmark before building, see 02-ui-roadmap.md). Revenue
    /// and COGS are read from the JournalEntry ledger (same source as every other
    /// financial figure in this app); Operating Expenses are read directly from
    /// OperatingExpense filtered on IncurredAt (the accrual date), not from the
    /// journal — the journal doesn't carry Category, and IncurredAt is the more
    /// precise "which period does this belong to" field for that domain anyway. No
    /// tax/PPN line — deliberately out of scope, see roadmap Phase 8.
    /// </summary>
    [HttpGet("profit-loss")]
    public async Task<ActionResult<ProfitLossResponse>> ProfitLoss([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from)
        {
            return BadRequest(new { message = "'to' must not be before 'from'." });
        }

        var rangeStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeEndExclusive = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);

        var revenueAccount = await _db.Accounts.SingleAsync(a => a.Code == "4000");
        var cogsAccount = await _db.Accounts.SingleAsync(a => a.Code == "5000");

        var revenue = await _db.JournalLines
            .Where(l => l.AccountId == revenueAccount.Id)
            .Join(_db.JournalEntries, l => l.JournalEntryId, j => j.Id, (l, j) => new { l.Debit, l.Credit, j.CreatedAt })
            .Where(x => x.CreatedAt >= rangeStart && x.CreatedAt < rangeEndExclusive)
            .SumAsync(x => x.Credit - x.Debit);

        var cogs = await _db.JournalLines
            .Where(l => l.AccountId == cogsAccount.Id)
            .Join(_db.JournalEntries, l => l.JournalEntryId, j => j.Id, (l, j) => new { l.Debit, l.Credit, j.CreatedAt })
            .Where(x => x.CreatedAt >= rangeStart && x.CreatedAt < rangeEndExclusive)
            .SumAsync(x => x.Debit - x.Credit);

        var grossProfit = revenue - cogs;
        var grossMarginPct = revenue != 0 ? grossProfit / revenue * 100 : 0;

        var expensesByCategory = await _db.OperatingExpenses
            .Where(e => e.IncurredAt >= from && e.IncurredAt <= to)
            .GroupBy(e => e.Category)
            .Select(g => new ProfitLossExpenseCategoryLine(g.Key, g.Sum(e => e.Amount)))
            .ToListAsync();

        var totalOperatingExpenses = expensesByCategory.Sum(l => l.Amount);
        var netProfit = grossProfit - totalOperatingExpenses;
        var netMarginPct = revenue != 0 ? netProfit / revenue * 100 : 0;

        return Ok(new ProfitLossResponse(
            from, to, revenue, cogs, grossProfit, grossMarginPct,
            expensesByCategory, totalOperatingExpenses, netProfit, netMarginPct));
    }

    /// <summary>
    /// Per-product gross margin — Revenue and COGS attributed back to each Product
    /// sold, not just an order-wide total. Prefers OrderItem.EstimatedCogs (the
    /// snapshot taken at Checkout, see Order.cs); for any OrderItem that predates
    /// that field (EstimatedCogs is null), falls back in the same request to
    /// today's Ingredient.AverageCost × Recipe × Quantity — less precise (today's
    /// cost, not the cost at the time of that sale) but keeps the report non-empty
    /// for historical data instead of silently dropping it. CogsIsEstimated on the
    /// response tells the frontend which rows include a fallback so it can be
    /// labeled, not hidden.
    /// </summary>
    [HttpGet("product-margin")]
    public async Task<ActionResult<ProductMarginResponse>> ProductMargin([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from)
        {
            return BadRequest(new { message = "'to' must not be before 'from'." });
        }

        var rangeStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeEndExclusive = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);

        var completedOrderIds = await _db.Orders
            .Where(o => o.Status == OrderStatus.Completed && o.CreatedAt >= rangeStart && o.CreatedAt < rangeEndExclusive)
            .Select(o => o.Id)
            .ToListAsync();

        var orderItems = await _db.OrderItems.Where(oi => completedOrderIds.Contains(oi.OrderId)).ToListAsync();

        var productIds = orderItems.Select(oi => oi.ProductId).Distinct().ToList();
        var productNames = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
        var recipeItems = await _db.RecipeItems.Where(r => productIds.Contains(r.ProductId)).ToListAsync();
        var ingredientIds = recipeItems.Select(r => r.IngredientId).Distinct().ToList();
        var averageCosts = await _db.Ingredients.Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.AverageCost);

        var items = new List<ProductMarginItem>();
        foreach (var group in orderItems.GroupBy(oi => oi.ProductId))
        {
            var quantitySold = group.Sum(oi => oi.Quantity);
            var revenue = group.Sum(oi => oi.Subtotal);
            var cogsIsEstimated = group.Any(oi => oi.EstimatedCogs is null);

            var cogs = group.Sum(oi => oi.EstimatedCogs ?? recipeItems
                .Where(r => r.ProductId == oi.ProductId)
                .Sum(r => r.Quantity * oi.Quantity * averageCosts.GetValueOrDefault(r.IngredientId, 0m)));

            var grossProfit = revenue - cogs;
            var grossMarginPct = revenue != 0 ? grossProfit / revenue * 100 : 0;

            items.Add(new ProductMarginItem(
                group.Key, productNames.GetValueOrDefault(group.Key, "?"), quantitySold,
                revenue, cogs, grossProfit, grossMarginPct, cogsIsEstimated));
        }

        items = items.OrderByDescending(i => i.GrossProfit).ToList();

        return Ok(new ProductMarginResponse(from, to, items));
    }
}
