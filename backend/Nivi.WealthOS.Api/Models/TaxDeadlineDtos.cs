namespace Nivi.WealthOS.Api.Models;

public record CreateTaxDeadlineRequest(
    Guid EntityId,
    string Name,
    DateOnly DueDate,
    string? Description);

public record UpdateTaxDeadlineRequest(
    string Name,
    DateOnly DueDate,
    string? Description);

public record TaxDeadlineResponse(
    Guid Id,
    Guid EntityId,
    string Name,
    DateOnly DueDate,
    string? Description);
