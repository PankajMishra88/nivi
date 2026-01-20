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
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly RbacService _rbacService;
    private readonly TransactionRules _transactionRules;
    private readonly CreditCardBalanceService _creditCardBalanceService;
    private readonly IAuditLogger _auditLogger;

    public TransactionsController(
        AppDbContext dbContext,
        ICurrentUserContext currentUser,
        RbacService rbacService,
        TransactionRules transactionRules,
        CreditCardBalanceService creditCardBalanceService,
        IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _rbacService = rbacService;
        _transactionRules = transactionRules;
        _creditCardBalanceService = creditCardBalanceService;
        _auditLogger = auditLogger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionResponse>>> GetTransactions(
        [FromQuery] Guid? entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

        IQueryable<Transaction> query = _dbContext.Transactions;

        if (entityId.HasValue)
        {
            await _rbacService.EnsureEntityPermissionAsync(_currentUser.UserId.Value, entityId.Value, PermissionAction.View);
            query = query.Where(tx => tx.EntityId == entityId.Value);
        }
        else
        {
            var roles = await _dbContext.UserRoles.Where(role => role.UserId == _currentUser.UserId.Value).ToListAsync();
            var isSuperAdmin = roles.Any(role => role.Role == RoleName.SuperAdmin && role.ScopeType == RoleScope.Tenant);

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
                query = query.Where(tx => accessibleEntityIds.Contains(tx.EntityId));
            }
        }

        var transactions = await query
            .OrderByDescending(tx => tx.Date)
            .ThenByDescending(tx => tx.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(tx => new TransactionResponse(
                tx.Id,
                tx.EntityId,
                tx.Type,
                tx.Status,
                tx.Date,
                tx.FromAccountId,
                tx.ToAccountId,
                tx.LoanId,
                tx.Category,
                tx.Description,
                tx.OriginalCurrency,
                tx.OriginalAmount,
                tx.FxRateUsed,
                tx.BaseAmount,
                tx.AdjustmentReason,
                tx.CreatedAtUtc))
            .ToListAsync();

        return Ok(transactions);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> CreateTransaction(CreateTransactionRequest request)
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

        var baseCurrency = tenant.BaseCurrency;
        var originalCurrency = request.OriginalCurrency.Trim().ToUpperInvariant();
        var fxRateUsed = request.FxRateUsed;

        if (string.IsNullOrWhiteSpace(originalCurrency))
        {
            return BadRequest("Original currency is required.");
        }

        if (!originalCurrency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase) && !fxRateUsed.HasValue)
        {
            var storedRate = await _dbContext.FxRates.FirstOrDefaultAsync(rate =>
                rate.Date == request.Date &&
                rate.BaseCurrency == baseCurrency &&
                rate.QuoteCurrency == originalCurrency);

            if (storedRate == null)
            {
                return BadRequest("FX rate required or stored rate missing for date.");
            }

            fxRateUsed = storedRate.Rate;
        }

        TransactionComputation computation;
        try
        {
            computation = _transactionRules.ValidateAndCompute(
                request.Type,
                request.FromAccountId,
                request.ToAccountId,
                baseCurrency,
                originalCurrency,
                request.OriginalAmount,
                fxRateUsed,
                request.AdjustmentReason);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        Account? fromAccount = null;
        Account? toAccount = null;

        if (request.FromAccountId.HasValue)
        {
            fromAccount = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.FromAccountId.Value);
            if (fromAccount == null || fromAccount.EntityId != request.EntityId)
            {
                return BadRequest("Invalid from account.");
            }
        }

        if (request.ToAccountId.HasValue)
        {
            toAccount = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.ToAccountId.Value);
            if (toAccount == null || toAccount.EntityId != request.EntityId)
            {
                return BadRequest("Invalid to account.");
            }
        }

        if (request.FromAccountId.HasValue && request.ToAccountId.HasValue && request.FromAccountId == request.ToAccountId)
        {
            return BadRequest("From and To account cannot be the same.");
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityId,
            Type = request.Type,
            Status = request.Status,
            Date = request.Date,
            FromAccountId = request.FromAccountId,
            ToAccountId = request.ToAccountId,
            LoanId = request.LoanId,
            Category = request.Category?.Trim(),
            Description = request.Description?.Trim(),
            OriginalCurrency = originalCurrency,
            OriginalAmount = request.OriginalAmount,
            FxRateUsed = computation.FxRateUsed,
            BaseAmount = computation.BaseAmount,
            AdjustmentReason = request.AdjustmentReason?.Trim(),
            CreatedBy = _currentUser.UserId.Value,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Transactions.Add(transaction);

        if (transaction.Status == TransactionStatus.Posted)
        {
            if (fromAccount != null && transaction.Type == TransactionType.Expense)
            {
                _creditCardBalanceService.ApplyExpense(fromAccount, transaction.BaseAmount);
            }

            if (toAccount != null && transaction.Type == TransactionType.Transfer)
            {
                _creditCardBalanceService.ApplyPayment(toAccount, transaction.BaseAmount);
            }
        }

        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("transaction.created", _currentUser.UserId.Value, _dbContext.TenantId!.Value, "Transaction", transaction.Id);

        return CreatedAtAction(nameof(GetTransactions), new { id = transaction.Id }, new TransactionResponse(
            transaction.Id,
            transaction.EntityId,
            transaction.Type,
            transaction.Status,
            transaction.Date,
            transaction.FromAccountId,
            transaction.ToAccountId,
            transaction.LoanId,
            transaction.Category,
            transaction.Description,
            transaction.OriginalCurrency,
            transaction.OriginalAmount,
            transaction.FxRateUsed,
            transaction.BaseAmount,
            transaction.AdjustmentReason,
            transaction.CreatedAtUtc));
    }
}
