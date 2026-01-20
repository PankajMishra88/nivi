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
[Route("api/entities")]
public class EntitiesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly IAuditLogger _auditLogger;

    public EntitiesController(
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
    public async Task<ActionResult<IReadOnlyList<EntityResponse>>> GetEntities([FromQuery] Guid? groupId = null)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var roles = await _dbContext.UserRoles.Where(role => role.UserId == _currentUser.UserId.Value).ToListAsync();
        var isSuperAdmin = roles.Any(role => role.Role == RoleName.SuperAdmin && role.ScopeType == RoleScope.Tenant);

        IQueryable<WealthEntity> query = _dbContext.Entities;
        if (!isSuperAdmin)
        {
            var accessibleGroupIds = roles.Where(role => role.ScopeType == RoleScope.Group && role.ScopeId.HasValue)
                .Select(role => role.ScopeId!.Value)
                .Distinct()
                .ToList();

            query = query.Where(entity => accessibleGroupIds.Contains(entity.GroupId));
        }

        if (groupId.HasValue)
        {
            query = query.Where(entity => entity.GroupId == groupId.Value);
        }

        var entities = await query.OrderBy(entity => entity.Name)
            .Select(entity => new EntityResponse(entity.Id, entity.GroupId, entity.Name, entity.Type))
            .ToListAsync();

        return Ok(entities);
    }

    [HttpPost]
    public async Task<ActionResult<EntityResponse>> CreateEntity(CreateEntityRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _rbacService.EnsureGroupPermissionAsync(_currentUser.UserId.Value, request.GroupId, PermissionAction.Create);

        var entity = new WealthEntity
        {
            Id = Guid.NewGuid(),
            GroupId = request.GroupId,
            Name = request.Name.Trim(),
            Type = request.Type,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Entities.Add(entity);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("entity.created", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Entity", entity.Id);

        return CreatedAtAction(nameof(GetEntities), new { id = entity.Id }, new EntityResponse(entity.Id, entity.GroupId, entity.Name, entity.Type));
    }
}
