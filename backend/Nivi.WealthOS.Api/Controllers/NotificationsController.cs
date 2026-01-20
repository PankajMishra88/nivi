using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Models;
using Nivi.WealthOS.Api.Services;

namespace Nivi.WealthOS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public NotificationsController(
        AppDbContext dbContext,
        ICurrentUserContext currentUser,
        IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> GetNotifications([FromQuery] bool includeRead = false)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var nowUtc = DateTime.UtcNow;
        var query = _dbContext.Notifications
            .Where(notification => notification.UserId == _currentUser.UserId.Value
                && notification.AvailableAtUtc <= nowUtc);

        if (!includeRead)
        {
            query = query.Where(notification => notification.ReadAtUtc == null);
        }

        var notifications = await query
            .OrderByDescending(notification => notification.AvailableAtUtc)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.Title,
                notification.Message,
                notification.SourceType,
                notification.SourceId,
                notification.AvailableAtUtc,
                notification.ReadAtUtc))
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var notification = await _dbContext.Notifications.FirstOrDefaultAsync(n =>
            n.Id == notificationId && n.UserId == _currentUser.UserId.Value);
        if (notification == null)
        {
            return NotFound();
        }

        if (!notification.ReadAtUtc.HasValue)
        {
            notification.ReadAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await _auditLogger.LogAsync("notification.read", _currentUser.UserId.Value, notification.TenantId, "Notification", notification.Id);
        }

        return NoContent();
    }
}
