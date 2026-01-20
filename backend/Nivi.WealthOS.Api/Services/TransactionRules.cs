using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Services;

public record TransactionComputation(decimal BaseAmount, decimal? FxRateUsed);

public class TransactionRules
{
    public TransactionComputation ValidateAndCompute(
        TransactionType type,
        Guid? fromAccountId,
        Guid? toAccountId,
        string baseCurrency,
        string originalCurrency,
        decimal originalAmount,
        decimal? fxRateUsed,
        string? adjustmentReason)
    {
        if (originalAmount <= 0)
        {
            throw new InvalidOperationException("Original amount must be greater than zero.");
        }

        switch (type)
        {
            case TransactionType.Expense:
                Require(fromAccountId, nameof(fromAccountId));
                RequireNull(toAccountId, nameof(toAccountId));
                break;
            case TransactionType.Income:
                Require(toAccountId, nameof(toAccountId));
                RequireNull(fromAccountId, nameof(fromAccountId));
                break;
            case TransactionType.Transfer:
                Require(fromAccountId, nameof(fromAccountId));
                Require(toAccountId, nameof(toAccountId));
                break;
            case TransactionType.Adjustment:
                if (string.IsNullOrWhiteSpace(adjustmentReason))
                {
                    throw new InvalidOperationException("Adjustment reason is required.");
                }
                break;
        }

        var normalizedBase = baseCurrency.ToUpperInvariant();
        var normalizedOriginal = originalCurrency.ToUpperInvariant();

        if (normalizedOriginal == normalizedBase)
        {
            if (fxRateUsed.HasValue && fxRateUsed.Value != 1)
            {
                throw new InvalidOperationException("FX rate should be omitted for base currency transactions.");
            }

            return new TransactionComputation(originalAmount, null);
        }

        if (!fxRateUsed.HasValue || fxRateUsed.Value <= 0)
        {
            throw new InvalidOperationException("FX rate is required for non-base currency transactions.");
        }

        var baseAmount = decimal.Round(originalAmount * fxRateUsed.Value, 2, MidpointRounding.AwayFromZero);
        return new TransactionComputation(baseAmount, fxRateUsed);
    }

    private static void Require(Guid? value, string field)
    {
        if (!value.HasValue || value == Guid.Empty)
        {
            throw new InvalidOperationException($"{field} is required.");
        }
    }

    private static void RequireNull(Guid? value, string field)
    {
        if (value.HasValue)
        {
            throw new InvalidOperationException($"{field} must be null.");
        }
    }
}
