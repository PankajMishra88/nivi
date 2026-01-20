namespace Nivi.WealthOS.Api.Services;

public class QuietHoursService
{
    private static readonly TimeSpan QuietStart = new(22, 0, 0);
    private static readonly TimeSpan QuietEnd = new(8, 0, 0);

    public DateTime GetNextAllowedUtc(DateTime utcNow, string timezoneId)
    {
        var localNow = ConvertToTenantTime(utcNow, timezoneId);
        var localTime = localNow.TimeOfDay;

        if (localTime >= QuietStart || localTime < QuietEnd)
        {
            var nextLocal = localTime < QuietEnd
                ? new DateTime(localNow.Year, localNow.Month, localNow.Day, QuietEnd.Hours, QuietEnd.Minutes, 0)
                : new DateTime(localNow.Year, localNow.Month, localNow.Day, QuietEnd.Hours, QuietEnd.Minutes, 0).AddDays(1);

            return ConvertToUtc(nextLocal, timezoneId);
        }

        return utcNow;
    }

    private static DateTime ConvertToTenantTime(DateTime utcNow, string timezoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return utcNow;
        }
        catch (InvalidTimeZoneException)
        {
            return utcNow;
        }
    }

    private static DateTime ConvertToUtc(DateTime local, string timezoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return TimeZoneInfo.ConvertTimeToUtc(local, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.SpecifyKind(local, DateTimeKind.Utc);
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.SpecifyKind(local, DateTimeKind.Utc);
        }
    }
}
