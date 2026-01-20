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
[Route("api/fx-rates")]
public class FxRatesController : ControllerBase
{
    private const int MaxPageSize = 200;
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly IAuditLogger _auditLogger;

    public FxRatesController(
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
    public async Task<ActionResult<IReadOnlyList<FxRateResponse>>> GetFxRates(
        [FromQuery] DateOnly? date,
        [FromQuery] string? baseCurrency,
        [FromQuery] string? quoteCurrency,
        [FromQuery] int pageSize = 50)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _rbacService.EnsureTenantPermissionAsync(_currentUser.UserId.Value, PermissionAction.View);

        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, MaxPageSize);

        IQueryable<FxRate> query = _dbContext.FxRates;
        if (date.HasValue)
        {
            query = query.Where(rate => rate.Date == date.Value);
        }

        if (!string.IsNullOrWhiteSpace(baseCurrency))
        {
            var normalized = baseCurrency.Trim().ToUpperInvariant();
            query = query.Where(rate => rate.BaseCurrency == normalized);
        }

        if (!string.IsNullOrWhiteSpace(quoteCurrency))
        {
            var normalized = quoteCurrency.Trim().ToUpperInvariant();
            query = query.Where(rate => rate.QuoteCurrency == normalized);
        }

        var rates = await query
            .OrderByDescending(rate => rate.Date)
            .ThenBy(rate => rate.QuoteCurrency)
            .Take(pageSize)
            .Select(rate => new FxRateResponse(
                rate.Id,
                rate.Date,
                rate.BaseCurrency,
                rate.QuoteCurrency,
                rate.Rate))
            .ToListAsync();

        return Ok(rates);
    }

    [HttpPost]
    public async Task<ActionResult<FxRateResponse>> CreateFxRate(CreateFxRateRequest request)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _rbacService.EnsureTenantPermissionAsync(_currentUser.UserId.Value, PermissionAction.Create);

        var baseCurrency = request.BaseCurrency.Trim().ToUpperInvariant();
        var quoteCurrency = request.QuoteCurrency.Trim().ToUpperInvariant();

        if (baseCurrency.Length != 3 || quoteCurrency.Length != 3 || baseCurrency == quoteCurrency)
        {
            return BadRequest("Invalid currency pair.");
        }

        if (request.Rate <= 0)
        {
            return BadRequest("FX rate must be positive.");
        }

        var exists = await _dbContext.FxRates.AnyAsync(rate =>
            rate.Date == request.Date &&
            rate.BaseCurrency == baseCurrency &&
            rate.QuoteCurrency == quoteCurrency);
        if (exists)
        {
            return Conflict("FX rate already exists for date and pair.");
        }

        var fxRate = new FxRate
        {
            Id = Guid.NewGuid(),
            Date = request.Date,
            BaseCurrency = baseCurrency,
            QuoteCurrency = quoteCurrency,
            Rate = request.Rate,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.FxRates.Add(fxRate);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("fx_rate.created", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "FxRate", fxRate.Id);

        return CreatedAtAction(nameof(GetFxRates), new { id = fxRate.Id }, new FxRateResponse(
            fxRate.Id,
            fxRate.Date,
            fxRate.BaseCurrency,
            fxRate.QuoteCurrency,
            fxRate.Rate));
    }
}
