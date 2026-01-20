namespace Nivi.WealthOS.Api.Models;

public record CreateInsurancePolicyRequest(
    Guid EntityId,
    string ProviderName,
    string PolicyNumber,
    string OriginalCurrency,
    decimal OriginalPremium,
    decimal? FxRateUsed,
    DateOnly RenewalDate);

public record UpdateInsurancePolicyRequest(
    string ProviderName,
    string PolicyNumber,
    string OriginalCurrency,
    decimal OriginalPremium,
    decimal? FxRateUsed,
    DateOnly RenewalDate);

public record InsurancePolicyResponse(
    Guid Id,
    Guid EntityId,
    string ProviderName,
    string PolicyNumber,
    string OriginalCurrency,
    decimal OriginalPremium,
    decimal? FxRateUsed,
    decimal BasePremium,
    DateOnly RenewalDate);
