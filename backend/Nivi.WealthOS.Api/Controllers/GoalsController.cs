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
[Route("api/goals")]
public class GoalsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly CurrencyConversionService _currencyConversionService;
    private readonly IAuditLogger _auditLogger;

    public GoalsController(
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
    public async Task<ActionResult<IReadOnlyList<GoalResponse>>> GetGoals([FromQuery] Guid? entityId = null)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        IQueryable<Goal> query = _dbContext.Goals;
        if (entityId.HasValue)
        {
            await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, entityId.Value, PermissionAction.View);
            query = query.Where(goal => goal.EntityId == entityId.Value);
        }
        else
        {
            var accessibleEntities = await GetAccessibleEntityIdsAsync(_currentUser.UserId.Value);
            query = query.Where(goal => accessibleEntities.Contains(goal.EntityId));
        }

        var goals = await query
            .OrderBy(goal => goal.CheckInDate)
            .Select(goal => new GoalResponse(
                goal.Id,
                goal.EntityId,
                goal.Name,
                goal.OriginalCurrency,
                goal.OriginalTargetAmount,
                goal.FxRateUsed,
                goal.BaseTargetAmount,
                goal.CheckInDate,
                goal.Status))
            .ToListAsync();

        return Ok(goals);
    }

    [HttpPost]
    public async Task<ActionResult<GoalResponse>> CreateGoal(CreateGoalRequest request)
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
                rate.Date == request.CheckInDate &&
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
                request.OriginalTargetAmount,
                fxRateUsed);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var goal = new Goal
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityId,
            Name = request.Name.Trim(),
            OriginalCurrency = currency,
            OriginalTargetAmount = request.OriginalTargetAmount,
            FxRateUsed = conversion.FxRateUsed,
            BaseTargetAmount = conversion.BaseAmount,
            CheckInDate = request.CheckInDate,
            Status = GoalStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Goals.Add(goal);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("goal.created", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Goal", goal.Id);

        return CreatedAtAction(nameof(GetGoals), new { id = goal.Id }, new GoalResponse(
            goal.Id,
            goal.EntityId,
            goal.Name,
            goal.OriginalCurrency,
            goal.OriginalTargetAmount,
            goal.FxRateUsed,
            goal.BaseTargetAmount,
            goal.CheckInDate,
            goal.Status));
    }

    [HttpPut("{goalId:guid}")]
    public async Task<ActionResult<GoalResponse>> UpdateGoal(Guid goalId, UpdateGoalRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var goal = await _dbContext.Goals.FirstOrDefaultAsync(g => g.Id == goalId);
        if (goal == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, goal.EntityId, PermissionAction.Edit);

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
                rate.Date == request.CheckInDate &&
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
                request.OriginalTargetAmount,
                fxRateUsed);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        goal.Name = request.Name.Trim();
        goal.OriginalCurrency = currency;
        goal.OriginalTargetAmount = request.OriginalTargetAmount;
        goal.FxRateUsed = conversion.FxRateUsed;
        goal.BaseTargetAmount = conversion.BaseAmount;
        goal.CheckInDate = request.CheckInDate;
        goal.Status = request.Status;

        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("goal.updated", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Goal", goal.Id);

        return Ok(new GoalResponse(
            goal.Id,
            goal.EntityId,
            goal.Name,
            goal.OriginalCurrency,
            goal.OriginalTargetAmount,
            goal.FxRateUsed,
            goal.BaseTargetAmount,
            goal.CheckInDate,
            goal.Status));
    }

    [HttpDelete("{goalId:guid}")]
    public async Task<IActionResult> DeleteGoal(Guid goalId)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var goal = await _dbContext.Goals.FirstOrDefaultAsync(g => g.Id == goalId);
        if (goal == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, goal.EntityId, PermissionAction.Delete);

        _dbContext.Goals.Remove(goal);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("goal.deleted", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Goal", goal.Id);

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
