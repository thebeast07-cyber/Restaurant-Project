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
