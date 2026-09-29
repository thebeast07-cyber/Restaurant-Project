namespace Restaurant.Api.Contracts;

public record SalesDailyReportResponse(
    DateOnly Date,
    int CompletedOrderCount,
    int VoidedOrderCount,
    decimal CashTotal,
    decimal NonCashTotal,
    decimal TotalRevenue);

public record StockLevelReportItem(Guid IngredientId, string Name, string Unit, decimal CurrentStock);

public record StockLevelReportResponse(DateTimeOffset AsOf, List<StockLevelReportItem> Items);
