namespace Restaurant.Api.Contracts;

public record CreatePromoCodeRequest(
    string Code,
    decimal PercentageOff,
    decimal? MinimumPurchase,
    DateOnly? ExpiresAt,
    int? UsageLimit);

public record UpdatePromoCodeRequest(
    decimal PercentageOff,
    decimal? MinimumPurchase,
    DateOnly? ExpiresAt,
    int? UsageLimit,
    bool IsActive);

public record PromoCodeResponse(
    Guid Id,
    string Code,
    decimal PercentageOff,
    decimal? MinimumPurchase,
    DateOnly? ExpiresAt,
    int? UsageLimit,
    int UsageCount,
    bool IsActive);
