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
[Route("api/insurance-policies")]
public class InsurancePoliciesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly CurrencyConversionService _currencyConversionService;
    private readonly IAuditLogger _auditLogger;

    public InsurancePoliciesController(
        AppDbContext dbContext,
        ICurrentUserContext currentUser,
        RbacService rbacService,
        CurrencyConversionService currencyConversionService,
        IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _rbacService = rbacService;
        _currencyConversionService = currencyConversionService;
        _auditLogger = auditLogger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InsurancePolicyResponse>>> GetPolicies([FromQuery] Guid? entityId = null)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        IQueryable<InsurancePolicy> query = _dbContext.InsurancePolicies;
        if (entityId.HasValue)
        {
            await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, entityId.Value, PermissionAction.View);
            query = query.Where(policy => policy.EntityId == entityId.Value);
        }
        else
        {
            var accessibleEntities = await GetAccessibleEntityIdsAsync(_currentUser.UserId.Value);
            query = query.Where(policy => accessibleEntities.Contains(policy.EntityId));
        }

        var policies = await query
            .OrderBy(policy => policy.RenewalDate)
            .Select(policy => new InsurancePolicyResponse(
                policy.Id,
                policy.EntityId,
                policy.ProviderName,
                policy.PolicyNumber,
                policy.OriginalCurrency,
                policy.OriginalPremium,
                policy.FxRateUsed,
                policy.BasePremium,
                policy.RenewalDate))
            .ToListAsync();

        return Ok(policies);
    }

    [HttpPost]
    public async Task<ActionResult<InsurancePolicyResponse>> CreatePolicy(CreateInsurancePolicyRequest request)
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

        var currency = request.OriginalCurrency.Trim().ToUpperInvariant();
        var fxRateUsed = request.FxRateUsed;
        if (!currency.Equals(tenant.BaseCurrency, StringComparison.OrdinalIgnoreCase) && !fxRateUsed.HasValue)
        {
            var storedRate = await _dbContext.FxRates.FirstOrDefaultAsync(rate =>
                rate.Date == request.RenewalDate &&
                rate.BaseCurrency == tenant.BaseCurrency &&
                rate.QuoteCurrency == currency);
            if (storedRate == null)
            {
                return BadRequest("FX rate required or stored rate missing for date.");
            }

            fxRateUsed = storedRate.Rate;
        }

        CurrencyConversionResult conversion;
        try
        {
            conversion = _currencyConversionService.ComputeBaseAmount(
                tenant.BaseCurrency,
                currency,
                request.OriginalPremium,
                fxRateUsed);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var policy = new InsurancePolicy
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityId,
            ProviderName = request.ProviderName.Trim(),
            PolicyNumber = request.PolicyNumber.Trim(),
            OriginalCurrency = currency,
            OriginalPremium = request.OriginalPremium,
            FxRateUsed = conversion.FxRateUsed,
            BasePremium = conversion.BaseAmount,
            RenewalDate = request.RenewalDate,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.InsurancePolicies.Add(policy);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("policy.created", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "InsurancePolicy", policy.Id);

        return CreatedAtAction(nameof(GetPolicies), new { id = policy.Id }, new InsurancePolicyResponse(
            policy.Id,
            policy.EntityId,
            policy.ProviderName,
            policy.PolicyNumber,
            policy.OriginalCurrency,
            policy.OriginalPremium,
            policy.FxRateUsed,
            policy.BasePremium,
            policy.RenewalDate));
    }

    [HttpPut("{policyId:guid}")]
    public async Task<ActionResult<InsurancePolicyResponse>> UpdatePolicy(Guid policyId, UpdateInsurancePolicyRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var policy = await _dbContext.InsurancePolicies.FirstOrDefaultAsync(p => p.Id == policyId);
        if (policy == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, policy.EntityId, PermissionAction.Edit);

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == _dbContext.TenantId);
        if (tenant == null)
        {
            return BadRequest("Tenant settings missing.");
        }

        var currency = request.OriginalCurrency.Trim().ToUpperInvariant();
        var fxRateUsed = request.FxRateUsed;
        if (!currency.Equals(tenant.BaseCurrency, StringComparison.OrdinalIgnoreCase) && !fxRateUsed.HasValue)
        {
            var storedRate = await _dbContext.FxRates.FirstOrDefaultAsync(rate =>
                rate.Date == request.RenewalDate &&
                rate.BaseCurrency == tenant.BaseCurrency &&
                rate.QuoteCurrency == currency);
            if (storedRate == null)
            {
                return BadRequest("FX rate required or stored rate missing for date.");
            }

            fxRateUsed = storedRate.Rate;
        }

        CurrencyConversionResult conversion;
        try
        {
            conversion = _currencyConversionService.ComputeBaseAmount(
                tenant.BaseCurrency,
                currency,
                request.OriginalPremium,
                fxRateUsed);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        policy.ProviderName = request.ProviderName.Trim();
        policy.PolicyNumber = request.PolicyNumber.Trim();
        policy.OriginalCurrency = currency;
        policy.OriginalPremium = request.OriginalPremium;
        policy.FxRateUsed = conversion.FxRateUsed;
        policy.BasePremium = conversion.BaseAmount;
        policy.RenewalDate = request.RenewalDate;

        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("policy.updated", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "InsurancePolicy", policy.Id);

        return Ok(new InsurancePolicyResponse(
            policy.Id,
            policy.EntityId,
            policy.ProviderName,
            policy.PolicyNumber,
            policy.OriginalCurrency,
            policy.OriginalPremium,
            policy.FxRateUsed,
            policy.BasePremium,
            policy.RenewalDate));
    }

    [HttpDelete("{policyId:guid}")]
    public async Task<IActionResult> DeletePolicy(Guid policyId)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var policy = await _dbContext.InsurancePolicies.FirstOrDefaultAsync(p => p.Id == policyId);
        if (policy == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, policy.EntityId, PermissionAction.Delete);

        _dbContext.InsurancePolicies.Remove(policy);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("policy.deleted", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "InsurancePolicy", policy.Id);

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
