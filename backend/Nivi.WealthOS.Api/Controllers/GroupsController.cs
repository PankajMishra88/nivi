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
[Route("api/groups")]
public class GroupsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly IAuditLogger _auditLogger;

    public GroupsController(
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
    public async Task<ActionResult<IReadOnlyList<GroupResponse>>> GetGroups()
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var userId = _currentUser.UserId.Value;
        var roles = await _dbContext.UserRoles.Where(role => role.UserId == userId).ToListAsync();
        var isSuperAdmin = roles.Any(role => role.Role == RoleName.SuperAdmin && role.ScopeType == RoleScope.Tenant);

        IQueryable<Group> query = _dbContext.Groups;
        if (!isSuperAdmin)
        {
            var groupIds = roles.Where(role => role.ScopeType == RoleScope.Group && role.ScopeId.HasValue)
                .Select(role => role.ScopeId!.Value)
                .Distinct()
                .ToList();

            query = query.Where(group => groupIds.Contains(group.Id));
        }

        var groups = await query.OrderBy(group => group.Name)
            .Select(group => new GroupResponse(group.Id, group.Name))
            .ToListAsync();

        return Ok(groups);
    }

    [HttpPost]
    public async Task<ActionResult<GroupResponse>> CreateGroup(CreateGroupRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _rbacService.EnsureTenantPermissionAsync(_currentUser.UserId.Value, PermissionAction.Create);

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("group.created", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Group", group.Id);

        return CreatedAtAction(nameof(GetGroups), new { id = group.Id }, new GroupResponse(group.Id, group.Name));
    }
}
