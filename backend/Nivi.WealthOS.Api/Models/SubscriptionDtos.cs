namespace Nivi.WealthOS.Api.Models;

public record CreateSubscriptionRequest(
    Guid EntityId,
    string Name,
    string OriginalCurrency,
    decimal OriginalAmount,
    decimal? FxRateUsed,
    DateOnly NextDueDate,
    string Frequency);

public record UpdateSubscriptionRequest(
    string Name,
    string OriginalCurrency,
    decimal OriginalAmount,
    decimal? FxRateUsed,
    DateOnly NextDueDate,
    string Frequency,
    bool IsActive);

public record SubscriptionResponse(
    Guid Id,
    Guid EntityId,
    string Name,
    string OriginalCurrency,
    decimal OriginalAmount,
    decimal? FxRateUsed,
    decimal BaseAmount,
    DateOnly NextDueDate,
    string Frequency,
    bool IsActive);
