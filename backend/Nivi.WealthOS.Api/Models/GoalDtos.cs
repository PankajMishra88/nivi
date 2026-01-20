using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Models;

public record CreateGoalRequest(
    Guid EntityId,
    string Name,
    string OriginalCurrency,
    decimal OriginalTargetAmount,
    decimal? FxRateUsed,
    DateOnly CheckInDate);

public record UpdateGoalRequest(
    string Name,
    string OriginalCurrency,
    decimal OriginalTargetAmount,
    decimal? FxRateUsed,
    DateOnly CheckInDate,
    GoalStatus Status);

public record GoalResponse(
    Guid Id,
    Guid EntityId,
    string Name,
    string OriginalCurrency,
    decimal OriginalTargetAmount,
    decimal? FxRateUsed,
    decimal BaseTargetAmount,
    DateOnly CheckInDate,
    GoalStatus Status);
