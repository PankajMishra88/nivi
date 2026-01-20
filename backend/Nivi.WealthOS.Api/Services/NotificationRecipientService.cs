using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Services;

public class NotificationRecipientService
{
    private readonly AppDbContext _dbContext;

    public NotificationRecipientService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Guid>> GetRecipientsForEntityAsync(Guid entityId)
    {
        var entity = await _dbContext.Entities.FirstOrDefaultAsync(e => e.Id == entityId);
        if (entity == null)
        {
            return Array.Empty<Guid>();
        }

        var roles = await _dbContext.UserRoles
            .Where(role =>
                (role.ScopeType == RoleScope.Entity && role.ScopeId == entityId)
                || (role.ScopeType == RoleScope.Group && role.ScopeId == entity.GroupId))
            .Select(role => role.UserId)
            .Distinct()
            .ToListAsync();

        return roles;
    }
}
