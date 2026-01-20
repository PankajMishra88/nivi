using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Enums;
using Nivi.WealthOS.Api.Models;
using Nivi.WealthOS.Api.Services;

namespace Nivi.WealthOS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly IAuditLogger _auditLogger;

    public SessionsController(
        AppDbContext dbContext,
        ICurrentUserContext currentUser,
        RbacService rbacService,
        IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _rbacService = rbacService;
        _auditLogger = auditLogger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> GetSessions()
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _rbacService.EnsureTenantPermissionAsync(_currentUser.UserId.Value, PermissionAction.View);

        var sessions = await _dbContext.UserSessions
            .OrderByDescending(session => session.CreatedAtUtc)
            .Select(session => new SessionResponse(
                session.Id,
                session.UserId,
                session.JwtId,
                session.CreatedAtUtc,
                session.ExpiresAtUtc,
                session.RevokedAtUtc,
                session.IpAddress,
                session.UserAgent))
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpPost("{sessionId:guid}/revoke")]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _rbacService.EnsureTenantPermissionAsync(_currentUser.UserId.Value, PermissionAction.Edit);

        var session = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null)
        {
            return NotFound();
        }

        session.RevokedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("session.revoked", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Session", session.Id);

        return NoContent();
    }
}
