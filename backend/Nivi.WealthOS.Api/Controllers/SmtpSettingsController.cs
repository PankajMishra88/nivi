using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Entities;
using Nivi.WealthOS.Api.Domain.Enums;
using Nivi.WealthOS.Api.Models;
using Nivi.WealthOS.Api.Services;

namespace Nivi.WealthOS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/smtp-settings")]
public class SmtpSettingsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly IAuditLogger _auditLogger;

    public SmtpSettingsController(
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
    public async Task<ActionResult<SmtpSettingsResponse>> GetSettings()
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _rbacService.EnsureTenantPermissionAsync(_currentUser.UserId.Value, PermissionAction.View);

        var settings = await _dbContext.TenantSmtpSettings.AsNoTracking().FirstOrDefaultAsync();
        if (settings == null)
        {
            return Ok(new SmtpSettingsResponse(string.Empty, 25, null, true, string.Empty));
        }

        return Ok(new SmtpSettingsResponse(
            settings.Host,
            settings.Port,
            settings.Username,
            settings.UseSsl,
            settings.DefaultFrom));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings(UpdateSmtpSettingsRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _rbacService.EnsureTenantPermissionAsync(_currentUser.UserId.Value, PermissionAction.Edit);

        var settings = await _dbContext.TenantSmtpSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new TenantSmtpSetting
            {
                Id = Guid.NewGuid(),
                Host = request.Host.Trim(),
                Port = request.Port,
                Username = request.Username?.Trim(),
                Password = request.Password,
                UseSsl = request.UseSsl,
                DefaultFrom = request.DefaultFrom.Trim(),
                UpdatedAtUtc = DateTime.UtcNow
            };
            _dbContext.TenantSmtpSettings.Add(settings);
        }
        else
        {
            settings.Host = request.Host.Trim();
            settings.Port = request.Port;
            settings.Username = request.Username?.Trim();
            settings.Password = request.Password;
            settings.UseSsl = request.UseSsl;
            settings.DefaultFrom = request.DefaultFrom.Trim();
            settings.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("smtp.updated", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "TenantSmtpSetting", settings.Id);

        return NoContent();
    }
}
