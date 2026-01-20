using Nivi.WealthOS.Api.Domain.Entities;
using Nivi.WealthOS.Api.Domain.Enums;
using Nivi.WealthOS.Api.Services;

namespace Nivi.WealthOS.Api.Tests;

public class CreditCardBalanceServiceTests
{
    private readonly CreditCardBalanceService _service = new();

    [Fact]
    public void ApplyExpenseAddsToUnbilled()
    {
        var account = new Account { Type = AccountType.CreditCard, UnbilledAmount = 100m };
        _service.ApplyExpense(account, 50m);
        Assert.Equal(150m, account.UnbilledAmount);
    }

    [Fact]
    public void ApplyPaymentReducesBilledThenUnbilled()
    {
        var account = new Account
        {
            Type = AccountType.CreditCard,
            BilledAmount = 200m,
            UnbilledAmount = 75m
        };

        _service.ApplyPayment(account, 250m);

        Assert.Equal(0m, account.BilledAmount);
        Assert.Equal(25m, account.UnbilledAmount);
    }
}
