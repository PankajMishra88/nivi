using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Entities;
using Nivi.WealthOS.Api.Options;

namespace Nivi.WealthOS.Api.Services;

public record EffectiveSmtpSettings(
    string Host,
    int Port,
    string? Username,
    string? Password,
    bool UseSsl,
    string DefaultFrom);

public class SmtpSettingsService
{
    private readonly AppDbContext _dbContext;
    private readonly SmtpOptions _defaultOptions;

    public SmtpSettingsService(AppDbContext dbContext, IOptions<SmtpOptions> options)
    {
        _dbContext = dbContext;
        _defaultOptions = options.Value;
    }

    public async Task<EffectiveSmtpSettings> GetSettingsAsync(Guid tenantId)
    {
        var overrideSettings = await _dbContext.TenantSmtpSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.TenantId == tenantId);

        if (overrideSettings == null)
        {
            return new EffectiveSmtpSettings(
                _defaultOptions.Host,
                _defaultOptions.Port,
                _defaultOptions.Username,
                _defaultOptions.Password,
                _defaultOptions.UseSsl,
                _defaultOptions.DefaultFrom);
        }

        return new EffectiveSmtpSettings(
            overrideSettings.Host,
            overrideSettings.Port,
            overrideSettings.Username,
            overrideSettings.Password,
            overrideSettings.UseSsl,
            overrideSettings.DefaultFrom);
    }
}
