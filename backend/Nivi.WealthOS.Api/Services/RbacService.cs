using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Services;

public class RbacService
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public RbacService(AppDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task EnsureTenantPermissionAsync(Guid userId, PermissionAction action)
    {
        var roles = await GetRolesAsync(userId);
        if (roles.Any(role => role.Role == RoleName.SuperAdmin && role.ScopeType == RoleScope.Tenant))
        {
            return;
        }

        throw new UnauthorizedAccessException("Tenant permission denied.");
    }

    public async Task EnsureGroupPermissionAsync(Guid userId, Guid groupId, PermissionAction action)
    {
        var roles = await GetRolesAsync(userId);
        if (roles.Any(role => role.Role == RoleName.SuperAdmin && role.ScopeType == RoleScope.Tenant))
        {
            return;
        }

        var tenant = await GetTenantAsync();
        var groupRoles = roles.Where(role => role.ScopeType == RoleScope.Group && role.ScopeId == groupId).ToList();
        if (groupRoles.Count == 0)
        {
            throw new UnauthorizedAccessException("Group permission denied.");
        }

        if (groupRoles.Any(role => IsAllowed(role.Role, action, tenant.AllowMemberDelete)))
        {
            return;
        }

        throw new UnauthorizedAccessException("Group permission denied.");
    }

    public async Task EnsureEntityPermissionAsync(Guid userId, Guid entityId, PermissionAction action)
    {
        var entity = await _dbContext.Entities.FirstOrDefaultAsync(e => e.Id == entityId);
        if (entity == null)
        {
            throw new InvalidOperationException("Entity not found.");
        }

        var roles = await GetRolesAsync(userId);
        if (roles.Any(role => role.Role == RoleName.SuperAdmin && role.ScopeType == RoleScope.Tenant))
        {
            return;
        }

        var tenant = await GetTenantAsync();

        var entityRoles = roles.Where(role => role.ScopeType == RoleScope.Entity && role.ScopeId == entityId).ToList();
        if (entityRoles.Any(role => IsAllowed(role.Role, action, tenant.AllowMemberDelete)))
        {
            return;
        }

        var groupRoles = roles.Where(role => role.ScopeType == RoleScope.Group && role.ScopeId == entity.GroupId).ToList();
        if (groupRoles.Any(role => IsAllowed(role.Role, action, tenant.AllowMemberDelete)))
        {
            return;
        }

        throw new UnauthorizedAccessException("Entity permission denied.");
    }

    private Task<List<Domain.Entities.UserRole>> GetRolesAsync(Guid userId)
    {
        return _dbContext.UserRoles.Where(role => role.UserId == userId).ToListAsync();
    }

    private async Task<Domain.Entities.Tenant> GetTenantAsync()
    {
        if (!_tenantContext.TenantId.HasValue)
        {
            throw new InvalidOperationException("Tenant context missing.");
        }

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId.Value);
        if (tenant == null)
        {
            throw new InvalidOperationException("Tenant not found.");
        }

        return tenant;
    }

    private static bool IsAllowed(RoleName role, PermissionAction action, bool allowMemberDelete)
    {
        if (role == RoleName.SuperAdmin || role == RoleName.GroupAdmin)
        {
            return true;
        }

        if (role == RoleName.Viewer)
        {
            return action == PermissionAction.View;
        }

        if (action == PermissionAction.Delete)
        {
            return allowMemberDelete;
        }

        return action is PermissionAction.View
            or PermissionAction.Create
            or PermissionAction.Edit
            or PermissionAction.Export
            or PermissionAction.Attachments;
    }
}
