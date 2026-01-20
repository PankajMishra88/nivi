using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Entities;
using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Services;

public class ReminderService
{
    private readonly AppDbContext _dbContext;
    private readonly QuietHoursService _quietHoursService;

    public ReminderService(AppDbContext dbContext, QuietHoursService quietHoursService)
    {
        _dbContext = dbContext;
        _quietHoursService = quietHoursService;
    }

    public async Task QueueReminderAsync(
        Tenant tenant,
        Guid userId,
        ReminderSourceType sourceType,
        Guid sourceId,
        DateOnly dueDate,
        string title,
        string message,
        bool sendEmail,
        bool repeatDaily)
    {
        var reminder = await _dbContext.Reminders.FirstOrDefaultAsync(r =>
            r.UserId == userId
            && r.SourceType == sourceType
            && r.SourceId == sourceId
            && r.DueDate == dueDate);

        if (reminder == null)
        {
            reminder = new Reminder
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SourceType = sourceType,
                SourceId = sourceId,
                DueDate = dueDate,
                Status = ReminderStatus.Pending,
                Message = message,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Reminders.Add(reminder);
        }

        if (reminder.Status == ReminderStatus.Dismissed)
        {
            return;
        }

        if (!repeatDaily && reminder.Status == ReminderStatus.Sent)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        if (reminder.LastNotifiedAtUtc.HasValue && reminder.LastNotifiedAtUtc.Value.Date == nowUtc.Date)
        {
            return;
        }

        var availableAtUtc = _quietHoursService.GetNextAllowedUtc(nowUtc, tenant.Timezone);
        _dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            SourceType = sourceType,
            SourceId = sourceId,
            AvailableAtUtc = availableAtUtc,
            SendEmail = sendEmail,
            CreatedAtUtc = nowUtc
        });

        reminder.LastNotifiedAtUtc = nowUtc;
        if (!repeatDaily)
        {
            reminder.Status = ReminderStatus.Sent;
        }
    }
}
