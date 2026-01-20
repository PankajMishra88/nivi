using Nivi.WealthOS.Api.Domain.Entities;
using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Services;

public class CreditCardBalanceService
{
    public void ApplyExpense(Account account, decimal baseAmount)
    {
        if (account.Type != AccountType.CreditCard || baseAmount <= 0)
        {
            return;
        }

        account.UnbilledAmount += baseAmount;
    }

    public void ApplyPayment(Account account, decimal baseAmount)
    {
        if (account.Type != AccountType.CreditCard || baseAmount <= 0)
        {
            return;
        }

        var remaining = baseAmount;
        if (account.BilledAmount > 0)
        {
            var applied = Math.Min(account.BilledAmount, remaining);
            account.BilledAmount -= applied;
            remaining -= applied;
        }

        if (remaining > 0 && account.UnbilledAmount > 0)
        {
            var applied = Math.Min(account.UnbilledAmount, remaining);
            account.UnbilledAmount -= applied;
        }
    }
}
