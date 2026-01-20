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
[Route("api/tax-deadlines")]
public class TaxDeadlinesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly IAuditLogger _auditLogger;

    public TaxDeadlinesController(
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
    public async Task<ActionResult<IReadOnlyList<TaxDeadlineResponse>>> GetDeadlines([FromQuery] Guid? entityId = null)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        IQueryable<TaxDeadline> query = _dbContext.TaxDeadlines;
        if (entityId.HasValue)
        {
            await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, entityId.Value, PermissionAction.View);
            query = query.Where(deadline => deadline.EntityId == entityId.Value);
        }
        else
        {
            var accessibleEntities = await GetAccessibleEntityIdsAsync(_currentUser.UserId.Value);
            query = query.Where(deadline => accessibleEntities.Contains(deadline.EntityId));
        }

        var deadlines = await query
            .OrderBy(deadline => deadline.DueDate)
            .Select(deadline => new TaxDeadlineResponse(
                deadline.Id,
                deadline.EntityId,
                deadline.Name,
                deadline.DueDate,
                deadline.Description))
            .ToListAsync();

        return Ok(deadlines);
    }

    [HttpPost]
    public async Task<ActionResult<TaxDeadlineResponse>> CreateDeadline(CreateTaxDeadlineRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, request.EntityId, PermissionAction.Create);

        var deadline = new TaxDeadline
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityId,
            Name = request.Name.Trim(),
            DueDate = request.DueDate,
            Description = request.Description?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.TaxDeadlines.Add(deadline);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("tax_deadline.created", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "TaxDeadline", deadline.Id);

        return CreatedAtAction(nameof(GetDeadlines), new { id = deadline.Id }, new TaxDeadlineResponse(
            deadline.Id,
            deadline.EntityId,
            deadline.Name,
            deadline.DueDate,
            deadline.Description));
    }

    [HttpPut("{deadlineId:guid}")]
    public async Task<ActionResult<TaxDeadlineResponse>> UpdateDeadline(Guid deadlineId, UpdateTaxDeadlineRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var deadline = await _dbContext.TaxDeadlines.FirstOrDefaultAsync(d => d.Id == deadlineId);
        if (deadline == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, deadline.EntityId, PermissionAction.Edit);

        deadline.Name = request.Name.Trim();
        deadline.DueDate = request.DueDate;
        deadline.Description = request.Description?.Trim();

        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("tax_deadline.updated", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "TaxDeadline", deadline.Id);

        return Ok(new TaxDeadlineResponse(
            deadline.Id,
            deadline.EntityId,
            deadline.Name,
            deadline.DueDate,
            deadline.Description));
    }

    [HttpDelete("{deadlineId:guid}")]
    public async Task<IActionResult> DeleteDeadline(Guid deadlineId)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var deadline = await _dbContext.TaxDeadlines.FirstOrDefaultAsync(d => d.Id == deadlineId);
        if (deadline == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, deadline.EntityId, PermissionAction.Delete);

        _dbContext.TaxDeadlines.Remove(deadline);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("tax_deadline.deleted", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "TaxDeadline", deadline.Id);

        return NoContent();
    }

    private async Task<List<Guid>> GetAccessibleEntityIdsAsync(Guid userId)
    {
        var roles = await _dbContext.UserRoles.Where(role => role.UserId == userId).ToListAsync();
        if (roles.Any(role => role.Role == RoleName.SuperAdmin && role.ScopeType == RoleScope.Tenant))
        {
            return await _dbContext.Entities.Select(entity => entity.Id).ToListAsync();
        }

        var entityIds = roles.Where(role => role.ScopeType == RoleScope.Entity && role.ScopeId.HasValue)
            .Select(role => role.ScopeId!.Value)
            .ToList();

        var groupIds = roles.Where(role => role.ScopeType == RoleScope.Group && role.ScopeId.HasValue)
            .Select(role => role.ScopeId!.Value)
            .ToList();

        if (groupIds.Count > 0)
        {
            var groupEntityIds = await _dbContext.Entities
                .Where(entity => groupIds.Contains(entity.GroupId))
                .Select(entity => entity.Id)
                .ToListAsync();

            entityIds.AddRange(groupEntityIds);
        }

        return entityIds.Distinct().ToList();
    }
}
