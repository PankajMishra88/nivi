using Nivi.WealthOS.Api.Services;

namespace Nivi.WealthOS.Api.Tests;

public class CurrencyConversionServiceTests
{
    private readonly CurrencyConversionService _service = new();

    [Fact]
    public void BaseCurrencyReturnsOriginalAmount()
    {
        var result = _service.ComputeBaseAmount("INR", "INR", 500m, null);
        Assert.Equal(500m, result.BaseAmount);
        Assert.Null(result.FxRateUsed);
    }

    [Fact]
    public void NonBaseCurrencyRequiresFxRate()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.ComputeBaseAmount("INR", "USD", 10m, null));
    }
}
