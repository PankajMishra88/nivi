using Nivi.WealthOS.Api.Domain.Enums;
using Nivi.WealthOS.Api.Services;

namespace Nivi.WealthOS.Api.Tests;

public class TransactionRulesTests
{
    private readonly TransactionRules _rules = new();

    [Fact]
    public void BaseCurrencyTransactionsDoNotRequireFx()
    {
        var result = _rules.ValidateAndCompute(
            TransactionType.Income,
            fromAccountId: null,
            toAccountId: Guid.NewGuid(),
            baseCurrency: "INR",
            originalCurrency: "INR",
            originalAmount: 1200m,
            fxRateUsed: null,
            adjustmentReason: null);

        Assert.Equal(1200m, result.BaseAmount);
        Assert.Null(result.FxRateUsed);
    }

    [Fact]
    public void NonBaseCurrencyRequiresFxRate()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _rules.ValidateAndCompute(
                TransactionType.Expense,
                fromAccountId: Guid.NewGuid(),
                toAccountId: null,
                baseCurrency: "INR",
                originalCurrency: "USD",
                originalAmount: 10m,
                fxRateUsed: null,
                adjustmentReason: null));

        Assert.Contains("FX rate", ex.Message);
    }

    [Fact]
    public void AdjustmentRequiresReason()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _rules.ValidateAndCompute(
                TransactionType.Adjustment,
                fromAccountId: Guid.NewGuid(),
                toAccountId: null,
                baseCurrency: "INR",
                originalCurrency: "INR",
                originalAmount: 50m,
                fxRateUsed: null,
                adjustmentReason: null));

        Assert.Contains("Adjustment reason", ex.Message);
    }

    [Fact]
    public void ExpenseRequiresFromAccount()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _rules.ValidateAndCompute(
                TransactionType.Expense,
                fromAccountId: null,
                toAccountId: null,
                baseCurrency: "INR",
                originalCurrency: "INR",
                originalAmount: 100m,
                fxRateUsed: null,
                adjustmentReason: null));

        Assert.Contains("fromAccountId", ex.Message);
    }
}
