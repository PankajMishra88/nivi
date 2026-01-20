using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Models;

public record CreateLoanRequest(
    Guid EntityId,
    LoanDirection Direction,
    string Name,
    Guid LenderUserId,
    string? BorrowerName,
    string OriginalCurrency,
    decimal OriginalAmount,
    decimal? FxRateUsed,
    DateOnly? NextDueDate,
    decimal? EmiAmount);

public record UpdateLoanRequest(
    string Name,
    string? BorrowerName,
    DateOnly? NextDueDate,
    decimal? EmiAmount,
    bool IsActive);

public record LoanResponse(
    Guid Id,
    Guid EntityId,
    LoanDirection Direction,
    string Name,
    Guid LenderUserId,
    string? BorrowerName,
    string OriginalCurrency,
    decimal OriginalAmount,
    decimal? FxRateUsed,
    decimal BaseAmount,
    DateOnly? NextDueDate,
    decimal? EmiAmount,
    bool IsActive);
