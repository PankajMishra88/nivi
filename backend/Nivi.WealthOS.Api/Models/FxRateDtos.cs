namespace Nivi.WealthOS.Api.Models;

public record CreateFxRateRequest(DateOnly Date, string BaseCurrency, string QuoteCurrency, decimal Rate);

public record FxRateResponse(
    Guid Id,
    DateOnly Date,
    string BaseCurrency,
    string QuoteCurrency,
    decimal Rate);
