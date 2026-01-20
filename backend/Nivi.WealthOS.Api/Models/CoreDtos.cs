using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Models;

public record CreateGroupRequest(string Name);
public record GroupResponse(Guid Id, string Name);

public record CreateEntityRequest(Guid GroupId, string Name, EntityType Type);
public record EntityResponse(Guid Id, Guid GroupId, string Name, EntityType Type);

public record CreateAccountRequest(
    Guid EntityId,
    string Name,
    AccountType Type,
    string Currency,
    decimal OpeningBalance,
    decimal? OpeningBalanceFxRateUsed,
    int? BillingCycleDay,
    int? DueDay);

public record AccountResponse(
    Guid Id,
    Guid EntityId,
    string Name,
    AccountType Type,
    string Currency,
    decimal OpeningBalance,
    decimal OpeningBalanceBaseAmount,
    decimal? OpeningBalanceFxRateUsed,
    int? BillingCycleDay,
    int? DueDay,
    decimal BilledAmount,
    decimal UnbilledAmount,
    DateOnly? LastStatementDate);

public record AccountBalanceResponse(
    Guid Id,
    Guid EntityId,
    string Name,
    AccountType Type,
    string Currency,
    decimal OpeningBalance,
    decimal OpeningBalanceBaseAmount,
    decimal PostedDeltaBaseAmount,
    decimal CurrentBalanceBaseAmount,
    decimal BilledAmount,
    decimal UnbilledAmount);

public record AccountReconciliationResponse(
    Guid AccountId,
    decimal OpeningBalanceBaseAmount,
    decimal PostedDeltaBaseAmount,
    decimal CurrentBalanceBaseAmount);

public record CreateAdjustmentRequest(decimal Amount, string Reason, DateOnly? Date, decimal? FxRateUsed);
