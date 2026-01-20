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
[Route("api/subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly CurrencyConversionService _currencyConversionService;
    private readonly IAuditLogger _auditLogger;

    public SubscriptionsController(
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
    public async Task<ActionResult<IReadOnlyList<SubscriptionResponse>>> GetSubscriptions([FromQuery] Guid? entityId = null)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        IQueryable<Subscription> query = _dbContext.Subscriptions;
        if (entityId.HasValue)
        {
            await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, entityId.Value, PermissionAction.View);
            query = query.Where(subscription => subscription.EntityId == entityId.Value);
        }
        else
        {
            var accessibleEntities = await GetAccessibleEntityIdsAsync(_currentUser.UserId.Value);
            query = query.Where(subscription => accessibleEntities.Contains(subscription.EntityId));
        }

        var subscriptions = await query
            .OrderBy(subscription => subscription.NextDueDate)
            .Select(subscription => new SubscriptionResponse(
                subscription.Id,
                subscription.EntityId,
                subscription.Name,
                subscription.OriginalCurrency,
                subscription.OriginalAmount,
                subscription.FxRateUsed,
                subscription.BaseAmount,
                subscription.NextDueDate,
                subscription.Frequency,
                subscription.IsActive))
            .ToListAsync();

        return Ok(subscriptions);
    }

    [HttpPost]
    public async Task<ActionResult<SubscriptionResponse>> CreateSubscription(CreateSubscriptionRequest request)
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
                rate.Date == request.NextDueDate &&
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
                request.OriginalAmount,
                fxRateUsed);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityId,
            Name = request.Name.Trim(),
            OriginalCurrency = currency,
            OriginalAmount = request.OriginalAmount,
            FxRateUsed = conversion.FxRateUsed,
            BaseAmount = conversion.BaseAmount,
            NextDueDate = request.NextDueDate,
            Frequency = request.Frequency.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("subscription.created", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Subscription", subscription.Id);

        return CreatedAtAction(nameof(GetSubscriptions), new { id = subscription.Id }, new SubscriptionResponse(
            subscription.Id,
            subscription.EntityId,
            subscription.Name,
            subscription.OriginalCurrency,
            subscription.OriginalAmount,
            subscription.FxRateUsed,
            subscription.BaseAmount,
            subscription.NextDueDate,
            subscription.Frequency,
            subscription.IsActive));
    }

    [HttpPut("{subscriptionId:guid}")]
    public async Task<ActionResult<SubscriptionResponse>> UpdateSubscription(Guid subscriptionId, UpdateSubscriptionRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var subscription = await _dbContext.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId);
        if (subscription == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, subscription.EntityId, PermissionAction.Edit);

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
                rate.Date == request.NextDueDate &&
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
                request.OriginalAmount,
                fxRateUsed);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        subscription.Name = request.Name.Trim();
        subscription.OriginalCurrency = currency;
        subscription.OriginalAmount = request.OriginalAmount;
        subscription.FxRateUsed = conversion.FxRateUsed;
        subscription.BaseAmount = conversion.BaseAmount;
        subscription.NextDueDate = request.NextDueDate;
        subscription.Frequency = request.Frequency.Trim();
        subscription.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("subscription.updated", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Subscription", subscription.Id);

        return Ok(new SubscriptionResponse(
            subscription.Id,
            subscription.EntityId,
            subscription.Name,
            subscription.OriginalCurrency,
            subscription.OriginalAmount,
            subscription.FxRateUsed,
            subscription.BaseAmount,
            subscription.NextDueDate,
            subscription.Frequency,
            subscription.IsActive));
    }

    [HttpDelete("{subscriptionId:guid}")]
    public async Task<IActionResult> DeleteSubscription(Guid subscriptionId)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var subscription = await _dbContext.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId);
        if (subscription == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, subscription.EntityId, PermissionAction.Delete);

        _dbContext.Subscriptions.Remove(subscription);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("subscription.deleted", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Subscription", subscription.Id);

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
