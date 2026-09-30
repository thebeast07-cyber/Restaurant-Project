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

public record SalesRangeResponse(DateOnly From, DateOnly To, List<SalesDailyReportResponse> Days);

public record WasteRangeItem(int Year, int Month, decimal TotalWasteValue);

public record WasteRangeResponse(DateOnly From, DateOnly To, List<WasteRangeItem> Months);

public record StockTrendPoint(DateOnly Date, decimal Balance);

public record StockTrendItem(
    Guid IngredientId,
    string Name,
    string Unit,
    decimal MinimumStock,
    int BelowMinimumDays,
    decimal NetChangePerDay,
    List<StockTrendPoint> Series);

public record StockTrendResponse(DateOnly From, DateOnly To, List<StockTrendItem> Items);
