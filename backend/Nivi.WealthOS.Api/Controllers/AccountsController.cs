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
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly IAuditLogger _auditLogger;

    public AccountsController(
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
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> GetAccounts([FromQuery] Guid? entityId = null)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var roles = await _dbContext.UserRoles.Where(role => role.UserId == _currentUser.UserId.Value).ToListAsync();
        var isSuperAdmin = roles.Any(role => role.Role == RoleName.SuperAdmin && role.ScopeType == RoleScope.Tenant);

        IQueryable<Account> query = _dbContext.Accounts;
        if (!isSuperAdmin)
        {
            var accessibleEntityIds = roles.Where(role => role.ScopeType == RoleScope.Entity && role.ScopeId.HasValue)
                .Select(role => role.ScopeId!.Value)
                .ToList();

            var accessibleGroupIds = roles.Where(role => role.ScopeType == RoleScope.Group && role.ScopeId.HasValue)
                .Select(role => role.ScopeId!.Value)
                .ToList();

            if (accessibleGroupIds.Count > 0)
            {
                var groupEntityIds = await _dbContext.Entities
                    .Where(entity => accessibleGroupIds.Contains(entity.GroupId))
                    .Select(entity => entity.Id)
                    .ToListAsync();

                accessibleEntityIds.AddRange(groupEntityIds);
            }

            accessibleEntityIds = accessibleEntityIds.Distinct().ToList();
            query = query.Where(account => accessibleEntityIds.Contains(account.EntityId));
        }

        if (entityId.HasValue)
        {
            query = query.Where(account => account.EntityId == entityId.Value);
        }

        var accounts = await query.OrderBy(account => account.Name)
            .Select(account => new AccountResponse(
                account.Id,
                account.EntityId,
                account.Name,
                account.Type,
                account.Currency,
                account.OpeningBalance,
                account.BillingCycleDay,
                account.DueDay))
            .ToListAsync();

        return Ok(accounts);
    }

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> CreateAccount(CreateAccountRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, request.EntityId, PermissionAction.Create);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityId,
            Name = request.Name.Trim(),
            Type = request.Type,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            OpeningBalance = request.OpeningBalance,
            BillingCycleDay = request.BillingCycleDay,
            DueDay = request.DueDay,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("account.created", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Account", account.Id);

        return CreatedAtAction(nameof(GetAccounts), new { id = account.Id }, new AccountResponse(
            account.Id,
            account.EntityId,
            account.Name,
            account.Type,
            account.Currency,
            account.OpeningBalance,
            account.BillingCycleDay,
            account.DueDay));
    }
}
