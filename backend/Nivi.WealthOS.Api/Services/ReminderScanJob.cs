using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Entities;
using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Services;

public class ReminderScanJob
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ReminderService _reminderService;
    private readonly NotificationRecipientService _recipientService;

    public ReminderScanJob(
        AppDbContext dbContext,
        ITenantContext tenantContext,
        ReminderService reminderService,
        NotificationRecipientService recipientService)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _reminderService = reminderService;
        _recipientService = recipientService;
    }

    public async Task RunAsync()
    {
        var tenants = await _dbContext.Tenants.AsNoTracking().ToListAsync();
        foreach (var tenant in tenants)
        {
            _tenantContext.SetTenantId(tenant.Id);
            var today = GetTenantDate(tenant.Timezone);

            await QueueCreditCardRemindersAsync(tenant, today);
            await QueueLoanRemindersAsync(tenant, today);
            await QueueInsuranceRemindersAsync(tenant, today);
            await QueueSubscriptionRemindersAsync(tenant, today);
            await QueueTaxDeadlineRemindersAsync(tenant, today);
            await QueueGoalRemindersAsync(tenant, today);

            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task QueueCreditCardRemindersAsync(Tenant tenant, DateOnly today)
    {
        var accounts = await _dbContext.Accounts
            .Where(account => account.Type == AccountType.CreditCard
                && account.BilledAmount > 0
                && account.DueDay.HasValue)
            .ToListAsync();

        foreach (var account in accounts)
        {
            var dueDate = GetDueDate(today, account.DueDay!.Value);
            if (today < dueDate)
            {
                continue;
            }

            var recipients = await _recipientService.GetRecipientsForEntityAsync(account.EntityId);
            foreach (var userId in recipients)
            {
                await _reminderService.QueueReminderAsync(
                    tenant,
                    userId,
                    ReminderSourceType.CreditCardBillDue,
                    account.Id,
                    dueDate,
                    "Credit card bill due",
                    $"Credit card bill due for {account.Name}. Billed amount: {account.BilledAmount:F2}.",
                    sendEmail: true,
                    repeatDaily: true);
            }
        }
    }

    private async Task QueueLoanRemindersAsync(Tenant tenant, DateOnly today)
    {
        var loans = await _dbContext.Loans
            .Where(loan => loan.IsActive && loan.NextDueDate.HasValue)
            .ToListAsync();

        foreach (var loan in loans)
        {
            var dueDate = loan.NextDueDate!.Value;
            if (today < dueDate)
            {
                continue;
            }

            if (loan.Direction == LoanDirection.Given)
            {
                var lenderId = loan.LenderUserId;
                await _reminderService.QueueReminderAsync(
                    tenant,
                    lenderId,
                    ReminderSourceType.LendingFollowUp,
                    loan.Id,
                    dueDate,
                    "Lending follow-up",
                    $"Loan follow-up for {loan.Name} is due.",
                    sendEmail: true,
                    repeatDaily: true);
            }
            else
            {
                var recipients = await _recipientService.GetRecipientsForEntityAsync(loan.EntityId);
                foreach (var userId in recipients)
                {
                    await _reminderService.QueueReminderAsync(
                        tenant,
                        userId,
                        ReminderSourceType.EmiDue,
                        loan.Id,
                        dueDate,
                        "EMI due",
                        $"EMI due for {loan.Name}.",
                        sendEmail: true,
                        repeatDaily: true);
                }
            }
        }
    }

    private async Task QueueInsuranceRemindersAsync(Tenant tenant, DateOnly today)
    {
        var policies = await _dbContext.InsurancePolicies
            .Where(policy => policy.RenewalDate <= today)
            .ToListAsync();

        foreach (var policy in policies)
        {
            var recipients = await _recipientService.GetRecipientsForEntityAsync(policy.EntityId);
            foreach (var userId in recipients)
            {
                await _reminderService.QueueReminderAsync(
                    tenant,
                    userId,
                    ReminderSourceType.InsuranceRenewal,
                    policy.Id,
                    policy.RenewalDate,
                    "Insurance renewal",
                    $"Insurance renewal due for policy {policy.PolicyNumber}.",
                    sendEmail: true,
                    repeatDaily: false);
            }
        }
    }

    private async Task QueueSubscriptionRemindersAsync(Tenant tenant, DateOnly today)
    {
        var subscriptions = await _dbContext.Subscriptions
            .Where(subscription => subscription.IsActive && subscription.NextDueDate <= today)
            .ToListAsync();

        foreach (var subscription in subscriptions)
        {
            var recipients = await _recipientService.GetRecipientsForEntityAsync(subscription.EntityId);
            foreach (var userId in recipients)
            {
                await _reminderService.QueueReminderAsync(
                    tenant,
                    userId,
                    ReminderSourceType.SubscriptionUpcoming,
                    subscription.Id,
                    subscription.NextDueDate,
                    "Subscription due",
                    $"Subscription payment due for {subscription.Name}.",
                    sendEmail: true,
                    repeatDaily: false);
            }
        }
    }

    private async Task QueueTaxDeadlineRemindersAsync(Tenant tenant, DateOnly today)
    {
        var deadlines = await _dbContext.TaxDeadlines
            .Where(deadline => deadline.DueDate <= today)
            .ToListAsync();

        foreach (var deadline in deadlines)
        {
            var recipients = await _recipientService.GetRecipientsForEntityAsync(deadline.EntityId);
            foreach (var userId in recipients)
            {
                await _reminderService.QueueReminderAsync(
                    tenant,
                    userId,
                    ReminderSourceType.TaxDeadline,
                    deadline.Id,
                    deadline.DueDate,
                    "Tax deadline",
                    $"Tax deadline due for {deadline.Name}.",
                    sendEmail: true,
                    repeatDaily: false);
            }
        }
    }

    private async Task QueueGoalRemindersAsync(Tenant tenant, DateOnly today)
    {
        var goals = await _dbContext.Goals
            .Where(goal => goal.Status == GoalStatus.Active && goal.CheckInDate <= today)
            .ToListAsync();

        foreach (var goal in goals)
        {
            var recipients = await _recipientService.GetRecipientsForEntityAsync(goal.EntityId);
            foreach (var userId in recipients)
            {
                await _reminderService.QueueReminderAsync(
                    tenant,
                    userId,
                    ReminderSourceType.GoalCheckIn,
                    goal.Id,
                    goal.CheckInDate,
                    "Goal check-in",
                    $"Goal check-in due for {goal.Name}.",
                    sendEmail: true,
                    repeatDaily: false);
            }
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

    private static DateOnly GetDueDate(DateOnly today, int dueDay)
    {
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var day = Math.Min(dueDay, daysInMonth);
        return new DateOnly(today.Year, today.Month, day);
    }
}
