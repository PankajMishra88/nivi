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
    private readonly CurrencyConversionService _currencyConversionService;
    private readonly AccountBalanceService _accountBalanceService;

    public AccountsController(
        AppDbContext dbContext,
        ICurrentUserContext currentUser,
        RbacService rbacService,
        IAuditLogger auditLogger,
        CurrencyConversionService currencyConversionService,
        AccountBalanceService accountBalanceService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _rbacService = rbacService;
        _auditLogger = auditLogger;
        _currencyConversionService = currencyConversionService;
        _accountBalanceService = accountBalanceService;
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
                account.OpeningBalanceBaseAmount,
                account.OpeningBalanceFxRateUsed,
                account.BillingCycleDay,
                account.DueDay,
                account.BilledAmount,
                account.UnbilledAmount,
                account.LastStatementDate))
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

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == _dbContext.TenantId);
        if (tenant == null)
        {
            return BadRequest("Tenant settings missing.");
        }

        var normalizedCurrency = request.Currency.Trim().ToUpperInvariant();
        CurrencyConversionResult conversion;
        try
        {
            conversion = _currencyConversionService.ComputeBaseAmount(
                tenant.BaseCurrency,
                normalizedCurrency,
                request.OpeningBalance,
                request.OpeningBalanceFxRateUsed);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityId,
            Name = request.Name.Trim(),
            Type = request.Type,
            Currency = normalizedCurrency,
            OpeningBalance = request.OpeningBalance,
            OpeningBalanceBaseAmount = conversion.BaseAmount,
            OpeningBalanceFxRateUsed = conversion.FxRateUsed,
            BillingCycleDay = request.BillingCycleDay,
            DueDay = request.DueDay,
            BilledAmount = 0,
            UnbilledAmount = 0,
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
            account.OpeningBalanceBaseAmount,
            account.OpeningBalanceFxRateUsed,
            account.BillingCycleDay,
            account.DueDay,
            account.BilledAmount,
            account.UnbilledAmount,
            account.LastStatementDate));
    }

    [HttpGet("balances")]
    public async Task<ActionResult<IReadOnlyList<AccountBalanceResponse>>> GetBalances([FromQuery] Guid? entityId = null)
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
            await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, entityId.Value, PermissionAction.View);
            query = query.Where(account => account.EntityId == entityId.Value);
        }

        var accounts = await query.ToListAsync();
        var deltas = await _accountBalanceService.GetPostedDeltasAsync(accounts.Select(account => account.Id).ToList());

        var balances = accounts.Select(account =>
        {
            var delta = deltas.TryGetValue(account.Id, out var value) ? value : 0m;
            var current = account.OpeningBalanceBaseAmount + delta;
            return new AccountBalanceResponse(
                account.Id,
                account.EntityId,
                account.Name,
                account.Type,
                account.Currency,
                account.OpeningBalance,
                account.OpeningBalanceBaseAmount,
                delta,
                current,
                account.BilledAmount,
                account.UnbilledAmount);
        }).ToList();

        return Ok(balances);
    }

    [HttpGet("{accountId:guid}/reconciliation")]
    public async Task<ActionResult<AccountReconciliationResponse>> GetReconciliation(Guid accountId)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, account.EntityId, PermissionAction.View);

        var deltas = await _accountBalanceService.GetPostedDeltasAsync(new[] { account.Id });
        var delta = deltas.TryGetValue(account.Id, out var value) ? value : 0m;
        var current = account.OpeningBalanceBaseAmount + delta;

        return Ok(new AccountReconciliationResponse(account.Id, account.OpeningBalanceBaseAmount, delta, current));
    }

    [HttpPost("{accountId:guid}/reconciliation/adjustment")]
    public async Task<IActionResult> CreateAdjustment(Guid accountId, CreateAdjustmentRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, account.EntityId, PermissionAction.Edit);

        if (request.Amount == 0)
        {
            return BadRequest("Adjustment amount must be non-zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest("Adjustment reason is required.");
        }

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == _dbContext.TenantId);
        if (tenant == null)
        {
            return BadRequest("Tenant settings missing.");
        }

        var adjustmentDate = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fxRateUsed = request.FxRateUsed;

        if (!account.Currency.Equals(tenant.BaseCurrency, StringComparison.OrdinalIgnoreCase) && !fxRateUsed.HasValue)
        {
            var storedRate = await _dbContext.FxRates.FirstOrDefaultAsync(rate =>
                rate.Date == adjustmentDate &&
                rate.BaseCurrency == tenant.BaseCurrency &&
                rate.QuoteCurrency == account.Currency);

            if (storedRate == null)
            {
                return BadRequest("FX rate required or stored rate missing for date.");
            }

            fxRateUsed = storedRate.Rate;
        }

        var originalAmount = Math.Abs(request.Amount);
        CurrencyConversionResult conversion;
        try
        {
            conversion = _currencyConversionService.ComputeBaseAmount(
                tenant.BaseCurrency,
                account.Currency,
                originalAmount,
                fxRateUsed);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var adjustment = new Transaction
        {
            Id = Guid.NewGuid(),
            EntityId = account.EntityId,
            Type = TransactionType.Adjustment,
            Status = TransactionStatus.Posted,
            Date = adjustmentDate,
            FromAccountId = request.Amount < 0 ? account.Id : null,
            ToAccountId = request.Amount > 0 ? account.Id : null,
            OriginalCurrency = account.Currency,
            OriginalAmount = originalAmount,
            FxRateUsed = conversion.FxRateUsed,
            BaseAmount = conversion.BaseAmount,
            AdjustmentReason = request.Reason.Trim(),
            CreatedBy = _currentUser.UserId.Value,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Transactions.Add(adjustment);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("reconciliation.adjustment", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Transaction", adjustment.Id);

        return NoContent();
    }
}
