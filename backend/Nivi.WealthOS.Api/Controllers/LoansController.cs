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
[Route("api/loans")]
public class LoansController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly CurrencyConversionService _currencyConversionService;
    private readonly IAuditLogger _auditLogger;

    public LoansController(
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
    public async Task<ActionResult<IReadOnlyList<LoanResponse>>> GetLoans([FromQuery] Guid? entityId = null)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        IQueryable<Loan> query = _dbContext.Loans;
        if (entityId.HasValue)
        {
            await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, entityId.Value, PermissionAction.View);
            query = query.Where(loan => loan.EntityId == entityId.Value);
        }
        else
        {
            var accessibleEntities = await GetAccessibleEntityIdsAsync(_currentUser.UserId.Value);
            query = query.Where(loan => accessibleEntities.Contains(loan.EntityId));
        }

        var loans = await query
            .OrderByDescending(loan => loan.CreatedAtUtc)
            .Select(loan => new LoanResponse(
                loan.Id,
                loan.EntityId,
                loan.Direction,
                loan.Name,
                loan.LenderUserId,
                loan.BorrowerName,
                loan.OriginalCurrency,
                loan.OriginalAmount,
                loan.FxRateUsed,
                loan.BaseAmount,
                loan.NextDueDate,
                loan.EmiAmount,
                loan.IsActive))
            .ToListAsync();

        return Ok(loans);
    }

    [HttpPost]
    public async Task<ActionResult<LoanResponse>> CreateLoan(CreateLoanRequest request)
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
                rate.Date == DateOnly.FromDateTime(DateTime.UtcNow)
                && rate.BaseCurrency == tenant.BaseCurrency
                && rate.QuoteCurrency == currency);
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

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityId,
            Direction = request.Direction,
            Name = request.Name.Trim(),
            LenderUserId = request.LenderUserId,
            BorrowerName = request.BorrowerName?.Trim(),
            OriginalCurrency = currency,
            OriginalAmount = request.OriginalAmount,
            FxRateUsed = conversion.FxRateUsed,
            BaseAmount = conversion.BaseAmount,
            NextDueDate = request.NextDueDate,
            EmiAmount = request.EmiAmount,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Loans.Add(loan);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("loan.created", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Loan", loan.Id);

        return CreatedAtAction(nameof(GetLoans), new { id = loan.Id }, new LoanResponse(
            loan.Id,
            loan.EntityId,
            loan.Direction,
            loan.Name,
            loan.LenderUserId,
            loan.BorrowerName,
            loan.OriginalCurrency,
            loan.OriginalAmount,
            loan.FxRateUsed,
            loan.BaseAmount,
            loan.NextDueDate,
            loan.EmiAmount,
            loan.IsActive));
    }

    [HttpPut("{loanId:guid}")]
    public async Task<ActionResult<LoanResponse>> UpdateLoan(Guid loanId, UpdateLoanRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == loanId);
        if (loan == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, loan.EntityId, PermissionAction.Edit);

        loan.Name = request.Name.Trim();
        loan.BorrowerName = request.BorrowerName?.Trim();
        loan.NextDueDate = request.NextDueDate;
        loan.EmiAmount = request.EmiAmount;
        loan.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("loan.updated", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Loan", loan.Id);

        return Ok(new LoanResponse(
            loan.Id,
            loan.EntityId,
            loan.Direction,
            loan.Name,
            loan.LenderUserId,
            loan.BorrowerName,
            loan.OriginalCurrency,
            loan.OriginalAmount,
            loan.FxRateUsed,
            loan.BaseAmount,
            loan.NextDueDate,
            loan.EmiAmount,
            loan.IsActive));
    }

    [HttpDelete("{loanId:guid}")]
    public async Task<IActionResult> DeleteLoan(Guid loanId)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == loanId);
        if (loan == null)
        {
            return NotFound();
        }

        await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, loan.EntityId, PermissionAction.Delete);

        _dbContext.Loans.Remove(loan);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("loan.deleted", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Loan", loan.Id);

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
