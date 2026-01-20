namespace Nivi.WealthOS.Api.Services;

public record CurrencyConversionResult(decimal BaseAmount, decimal? FxRateUsed);

public class CurrencyConversionService
{
    public CurrencyConversionResult ComputeBaseAmount(
        string baseCurrency,
        string originalCurrency,
        decimal originalAmount,
        decimal? fxRateUsed)
    {
        if (originalAmount < 0)
        {
            throw new InvalidOperationException("Amount must be non-negative.");
        }

        var normalizedBase = baseCurrency.ToUpperInvariant();
        var normalizedOriginal = originalCurrency.ToUpperInvariant();

        if (normalizedBase == normalizedOriginal)
        {
            if (fxRateUsed.HasValue && fxRateUsed.Value != 1)
            {
                throw new InvalidOperationException("FX rate should be omitted for base currency amounts.");
            }

            return new CurrencyConversionResult(originalAmount, null);
        }

        if (!fxRateUsed.HasValue || fxRateUsed.Value <= 0)
        {
            throw new InvalidOperationException("FX rate is required for non-base currency amounts.");
        }

        var baseAmount = decimal.Round(originalAmount * fxRateUsed.Value, 2, MidpointRounding.AwayFromZero);
        return new CurrencyConversionResult(baseAmount, fxRateUsed);
    }
}
