using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Services;

public class AccountBalanceService
{
    private readonly AppDbContext _dbContext;

    public AccountBalanceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Dictionary<Guid, decimal>> GetPostedDeltasAsync(IReadOnlyCollection<Guid> accountIds)
    {
        if (accountIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var fromDeltas = await _dbContext.Transactions
            .Where(tx => tx.Status == TransactionStatus.Posted
                && tx.FromAccountId.HasValue
                && accountIds.Contains(tx.FromAccountId.Value))
            .GroupBy(tx => tx.FromAccountId!.Value)
            .Select(group => new { AccountId = group.Key, Delta = -group.Sum(tx => tx.BaseAmount) })
            .ToListAsync();

        var toDeltas = await _dbContext.Transactions
            .Where(tx => tx.Status == TransactionStatus.Posted
                && tx.ToAccountId.HasValue
                && accountIds.Contains(tx.ToAccountId.Value))
            .GroupBy(tx => tx.ToAccountId!.Value)
            .Select(group => new { AccountId = group.Key, Delta = group.Sum(tx => tx.BaseAmount) })
            .ToListAsync();

        var deltas = new Dictionary<Guid, decimal>();
        foreach (var delta in fromDeltas.Concat(toDeltas))
        {
            if (deltas.TryGetValue(delta.AccountId, out var existing))
            {
                deltas[delta.AccountId] = existing + delta.Delta;
            }
            else
            {
                deltas[delta.AccountId] = delta.Delta;
            }
        }

        return deltas;
    }
}
