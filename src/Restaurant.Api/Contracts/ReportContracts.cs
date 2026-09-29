namespace Restaurant.Api.Contracts;

public record SalesDailyReportResponse(
    DateOnly Date,
    int CompletedOrderCount,
    int VoidedOrderCount,
    decimal CashTotal,
    decimal NonCashTotal,
    decimal TotalRevenue);

public record StockLevelReportItem(Guid IngredientId, string Name, string Unit, decimal CurrentStock, decimal MinimumStock, bool IsBelowMinimum);

public record StockLevelReportResponse(DateTimeOffset AsOf, int BelowMinimumCount, List<StockLevelReportItem> Items);

public record WasteReportItem(Guid IngredientId, string Name, string Unit, decimal QuantityWasted, decimal WasteValue);

public record WasteReportResponse(int Year, int Month, decimal TotalWasteValue, List<WasteReportItem> Items);
