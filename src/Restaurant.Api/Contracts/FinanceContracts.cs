using Restaurant.Domain.Finance;

namespace Restaurant.Api.Contracts;

public record CreateOperatingExpenseRequest(ExpenseCategory Category, string Description, decimal Amount, DateOnly IncurredAt);

public record OperatingExpenseResponse(
    Guid Id,
    ExpenseCategory Category,
    string Description,
    decimal Amount,
    DateOnly IncurredAt,
    decimal AmountPaid,
    decimal RemainingBalance,
    ExpensePaymentStatus PaymentStatus,
    Guid JournalEntryId);

public record RecordExpensePaymentRequest(decimal Amount);

public record ExpensePaymentResponse(
    Guid OperatingExpenseId,
    decimal AmountPaid,
    decimal RemainingBalance,
    ExpensePaymentStatus PaymentStatus,
    Guid JournalEntryId);

public record ProfitLossExpenseCategoryLine(ExpenseCategory Category, decimal Amount);

public record ProfitLossResponse(
    DateOnly From,
    DateOnly To,
    decimal Revenue,
    decimal Cogs,
    decimal GrossProfit,
    decimal GrossMarginPct,
    List<ProfitLossExpenseCategoryLine> OperatingExpenses,
    decimal TotalOperatingExpenses,
    decimal NetProfit,
    decimal NetMarginPct);

public record ProductMarginItem(
    Guid ProductId,
    string ProductName,
    int QuantitySold,
    decimal Revenue,
    decimal Cogs,
    decimal GrossProfit,
    decimal GrossMarginPct,
    bool CogsIsEstimated);

public record ProductMarginResponse(DateOnly From, DateOnly To, List<ProductMarginItem> Items);

public record CashFlowSourceLine(string Source, decimal CashIn, decimal CashOut);

public record CashFlowResponse(
    DateOnly From,
    DateOnly To,
    decimal OpeningCash,
    decimal CashIn,
    decimal CashOut,
    decimal NetCashFlow,
    decimal ClosingCash,
    List<CashFlowSourceLine> Sources);

public record ApAgingItem(
    string Type,
    string Name,
    Guid ReferenceId,
    DateOnly IncurredDate,
    decimal OutstandingAmount,
    int DaysOutstanding,
    string Bucket);

public record ApAgingResponse(
    DateOnly AsOf,
    decimal TotalOutstanding,
    decimal Bucket0To30,
    decimal Bucket31To60,
    decimal Bucket61To90,
    decimal BucketOver90,
    List<ApAgingItem> Items);

public record BalanceSheetLine(string Code, string Name, decimal Balance);

public record BalanceSheetResponse(
    DateOnly AsOf,
    List<BalanceSheetLine> Assets,
    decimal TotalAssets,
    List<BalanceSheetLine> Liabilities,
    decimal TotalLiabilities,
    decimal RetainedEarnings,
    decimal TotalEquity,
    decimal TotalLiabilitiesAndEquity,
    bool IsBalanced);
