using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Models;

public record NotificationResponse(
    Guid Id,
    string Title,
    string Message,
    ReminderSourceType SourceType,
    Guid? SourceId,
    DateTime AvailableAtUtc,
    DateTime? ReadAtUtc);
