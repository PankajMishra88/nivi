using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;

namespace Nivi.WealthOS.Api.Services;

public class NotificationDispatchJob
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly EmailService _emailService;
    private readonly IAuditLogger _auditLogger;

    public NotificationDispatchJob(
        AppDbContext dbContext,
        ITenantContext tenantContext,
        EmailService emailService,
        IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _emailService = emailService;
        _auditLogger = auditLogger;
    }

    public async Task RunAsync()
    {
        var tenants = await _dbContext.Tenants.AsNoTracking().ToListAsync();
        foreach (var tenant in tenants)
        {
            _tenantContext.SetTenantId(tenant.Id);
            await DispatchForTenantAsync(tenant.Id);
        }
    }

    private async Task DispatchForTenantAsync(Guid tenantId)
    {
        var nowUtc = DateTime.UtcNow;
        var notifications = await _dbContext.Notifications
            .Where(notification => notification.SendEmail
                && notification.EmailSentAtUtc == null
                && notification.AvailableAtUtc <= nowUtc)
            .OrderBy(notification => notification.AvailableAtUtc)
            .Take(100)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == notification.UserId);
            if (user == null)
            {
                continue;
            }

            await _emailService.SendAsync(
                tenantId,
                user.Email,
                notification.Title,
                notification.Message);

            notification.EmailSentAtUtc = DateTime.UtcNow;
            await _auditLogger.LogAsync("notification.email_sent", notification.UserId, tenantId, "Notification", notification.Id);
        }

        await _dbContext.SaveChangesAsync();
    }
}
