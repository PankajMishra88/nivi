using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Entities;
using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Services;

public class CreditCardBillingJob
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreditCardBillingJob(AppDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task RunAsync()
    {
        var tenants = await _dbContext.Tenants.AsNoTracking().ToListAsync();
        foreach (var tenant in tenants)
        {
            _tenantContext.SetTenantId(tenant.Id);
            var statementDate = GetTenantDate(tenant.Timezone);
            var accounts = await _dbContext.Accounts
                .Where(account => account.Type == AccountType.CreditCard && account.BillingCycleDay.HasValue)
                .ToListAsync();

            if (accounts.Count == 0)
            {
                continue;
            }

            var logs = new List<AuditLog>();
            foreach (var account in accounts)
            {
                if (account.BillingCycleDay != statementDate.Day)
                {
                    continue;
                }

                if (account.LastStatementDate.HasValue && account.LastStatementDate.Value == statementDate)
                {
                    continue;
                }

                if (account.UnbilledAmount > 0)
                {
                    var movedAmount = account.UnbilledAmount;
                    account.BilledAmount += movedAmount;
                    account.UnbilledAmount = 0;
                    logs.Add(new AuditLog
                    {
                        TenantId = tenant.Id,
                        Action = "credit_card.statement_rollover",
                        EntityType = "Account",
                        EntityId = account.Id,
                        MetadataJson = JsonSerializer.Serialize(new { amount = movedAmount })
                    });
                }

                account.LastStatementDate = statementDate;
            }

            if (logs.Count > 0)
            {
                _dbContext.AuditLogs.AddRange(logs);
            }

            await _dbContext.SaveChangesAsync();
        }
    }

    private static DateOnly GetTenantDate(string timezoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            return DateOnly.FromDateTime(local);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }
}
