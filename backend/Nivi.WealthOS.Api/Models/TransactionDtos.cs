using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Models;

public record CreateTransactionRequest(
    Guid EntityId,
    TransactionType Type,
    TransactionStatus Status,
    DateOnly Date,
    Guid? FromAccountId,
    Guid? ToAccountId,
    Guid? LoanId,
    string? Category,
    string? Description,
    string OriginalCurrency,
    decimal OriginalAmount,
    decimal? FxRateUsed,
    string? AdjustmentReason);

public record TransactionResponse(
    Guid Id,
    Guid EntityId,
    TransactionType Type,
    TransactionStatus Status,
    DateOnly Date,
    Guid? FromAccountId,
    Guid? ToAccountId,
    Guid? LoanId,
    string? Category,
    string? Description,
    string OriginalCurrency,
    decimal OriginalAmount,
    decimal? FxRateUsed,
    decimal BaseAmount,
    string? AdjustmentReason,
    DateTime CreatedAtUtc);
