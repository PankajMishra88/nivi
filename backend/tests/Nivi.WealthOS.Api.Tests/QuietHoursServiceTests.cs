using Nivi.WealthOS.Api.Services;

namespace Nivi.WealthOS.Api.Tests;

public class QuietHoursServiceTests
{
    [Fact]
    public void ReturnsNowOutsideQuietHours()
    {
        var service = new QuietHoursService();
        var utc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var result = service.GetNextAllowedUtc(utc, "UTC");
        Assert.Equal(utc, result);
    }
}
