using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Data;
using Nivi.WealthOS.Api.Domain.Entities;
using Nivi.WealthOS.Api.Domain.Enums;
using Nivi.WealthOS.Api.Models;
using Nivi.WealthOS.Api.Services;

namespace Nivi.WealthOS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const string IssuerName = "Nivi Wealth OS";

    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly PasswordHasher _passwordHasher;
    private readonly TotpService _totpService;
    private readonly RecoveryCodeService _recoveryCodeService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IAuditLogger _auditLogger;

    public AuthController(
        AppDbContext dbContext,
        ITenantContext tenantContext,
        PasswordHasher passwordHasher,
        TotpService totpService,
        RecoveryCodeService recoveryCodeService,
        JwtTokenService jwtTokenService,
        IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _passwordHasher = passwordHasher;
        _totpService = totpService;
        _recoveryCodeService = recoveryCodeService;
        _jwtTokenService = jwtTokenService;
        _auditLogger = auditLogger;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<RegisterTenantResponse>> Register(RegisterTenantRequest request)
    {
        var slug = request.TenantSlug.Trim().ToLowerInvariant();
        if (await _dbContext.Tenants.AnyAsync(t => t.Slug == slug))
        {
            return Conflict("Tenant slug already exists.");
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.TenantName.Trim(),
            Slug = slug,
            BaseCurrency = string.IsNullOrWhiteSpace(request.BaseCurrency) ? "INR" : request.BaseCurrency.Trim().ToUpperInvariant(),
            Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Kolkata" : request.Timezone.Trim(),
            AllowMemberDelete = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = request.AdminEmail.Trim().ToLowerInvariant(),
            DisplayName = request.AdminName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.AdminPassword),
            IsActive = true,
            MfaEnabled = false,
            MfaSecret = _totpService.GenerateSecret(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var group = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = $"{tenant.Name} Primary",
            CreatedAtUtc = DateTime.UtcNow
        };

        var roles = new[]
        {
            new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = user.Id,
                Role = RoleName.SuperAdmin,
                ScopeType = RoleScope.Tenant,
                ScopeId = null,
                CreatedAtUtc = DateTime.UtcNow
            },
            new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = user.Id,
                Role = RoleName.GroupAdmin,
                ScopeType = RoleScope.Group,
                ScopeId = group.Id,
                CreatedAtUtc = DateTime.UtcNow
            }
        };

        _dbContext.Tenants.Add(tenant);
        _tenantContext.SetTenantId(tenant.Id);
        _dbContext.Users.Add(user);
        _dbContext.Groups.Add(group);
        _dbContext.UserRoles.AddRange(roles);
        await _dbContext.SaveChangesAsync();

        var recoveryCodes = _recoveryCodeService.GenerateCodes();
        var recoveryEntities = recoveryCodes.Select(code => new UserRecoveryCode
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            CodeHash = _recoveryCodeService.HashCode(code),
            CreatedAtUtc = DateTime.UtcNow
        });
        _dbContext.UserRecoveryCodes.AddRange(recoveryEntities);
        await _dbContext.SaveChangesAsync();

        await _auditLogger.LogAsync("auth.register", user.Id, tenant.Id, "Tenant", tenant.Id, new { tenant.Slug });

        return Ok(new RegisterTenantResponse(
            tenant.Slug,
            user.Id,
            user.MfaSecret ?? string.Empty,
            _totpService.BuildOtpAuthUrl(IssuerName, user.Email, user.MfaSecret ?? string.Empty),
            recoveryCodes));
    }

    [HttpPost("confirm-mfa")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmMfa(ConfirmMfaRequest request)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Slug == request.TenantSlug.Trim().ToLowerInvariant());
        if (tenant == null)
        {
            return NotFound("Tenant not found.");
        }

        _tenantContext.SetTenantId(tenant.Id);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        if (string.IsNullOrWhiteSpace(user.MfaSecret) || !_totpService.VerifyCode(user.MfaSecret, request.Code))
        {
            await _auditLogger.LogAsync("auth.mfa_failed", user.Id, tenant.Id, "User", user.Id);
            return Unauthorized("Invalid MFA code.");
        }

        user.MfaEnabled = true;
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("auth.mfa_confirmed", user.Id, tenant.Id, "User", user.Id);

        return NoContent();
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Slug == request.TenantSlug.Trim().ToLowerInvariant());
        if (tenant == null)
        {
            return Unauthorized("Invalid credentials.");
        }

        _tenantContext.SetTenantId(tenant.Id);
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !user.IsActive)
        {
            await _auditLogger.LogAsync("auth.login_failed", null, tenant.Id, "User", null, new { email });
            return Unauthorized("Invalid credentials.");
        }

        if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > DateTime.UtcNow)
        {
            await _auditLogger.LogAsync("auth.login_locked", user.Id, tenant.Id, "User", user.Id);
            return StatusCode(StatusCodes.Status423Locked, "Account is locked.");
        }

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            user.FailedLoginCount += 1;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
            }

            await _dbContext.SaveChangesAsync();
            await _auditLogger.LogAsync("auth.login_failed", user.Id, tenant.Id, "User", user.Id);
            return Unauthorized("Invalid credentials.");
        }

        if (!user.MfaEnabled)
        {
            return Forbid("MFA setup required.");
        }

        if (!string.IsNullOrWhiteSpace(request.TotpCode))
        {
            if (string.IsNullOrWhiteSpace(user.MfaSecret) || !_totpService.VerifyCode(user.MfaSecret, request.TotpCode))
            {
                await _auditLogger.LogAsync("auth.login_failed", user.Id, tenant.Id, "User", user.Id, new { reason = "mfa" });
                return Unauthorized("Invalid MFA code.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            var recoveryCodes = await _dbContext.UserRecoveryCodes
                .Where(code => code.UserId == user.Id && code.UsedAtUtc == null)
                .ToListAsync();

            var matched = recoveryCodes.FirstOrDefault(code => _recoveryCodeService.VerifyHashedCode(code.CodeHash, request.RecoveryCode));
            if (matched == null)
            {
                await _auditLogger.LogAsync("auth.login_failed", user.Id, tenant.Id, "User", user.Id, new { reason = "recovery" });
                return Unauthorized("Invalid recovery code.");
            }

            matched.UsedAtUtc = DateTime.UtcNow;
        }
        else
        {
            return Unauthorized("MFA code required.");
        }

        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow;

        var tokenResult = _jwtTokenService.CreateToken(tenant.Id, user.Id, user.Email);
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            JwtId = tokenResult.JwtId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = tokenResult.ExpiresAtUtc
        };

        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("auth.login_success", user.Id, tenant.Id, "User", user.Id);

        return Ok(new LoginResponse(
            tokenResult.Token,
            tokenResult.ExpiresAtUtc,
            new UserSummary(user.Id, user.Email, user.DisplayName)));
    }

    [HttpPost("recovery-codes/regenerate")]
    [EnableRateLimiting("auth")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<string>>> RegenerateRecoveryCodes(RegenerateRecoveryCodesRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return Unauthorized();
        }

        var userId = User.FindFirst("user_id")?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == parsedUserId);
        if (user == null || string.IsNullOrWhiteSpace(user.MfaSecret))
        {
            return Unauthorized();
        }

        if (!_totpService.VerifyCode(user.MfaSecret, request.TotpCode))
        {
            await _auditLogger.LogAsync("auth.recovery_regen_failed", user.Id, tenantId.Value, "User", user.Id);
            return Unauthorized("Invalid MFA code.");
        }

        var existingCodes = await _dbContext.UserRecoveryCodes.Where(code => code.UserId == user.Id && code.UsedAtUtc == null).ToListAsync();
        foreach (var code in existingCodes)
        {
            code.UsedAtUtc = DateTime.UtcNow;
        }

        var recoveryCodes = _recoveryCodeService.GenerateCodes();
        var recoveryEntities = recoveryCodes.Select(code => new UserRecoveryCode
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            UserId = user.Id,
            CodeHash = _recoveryCodeService.HashCode(code),
            CreatedAtUtc = DateTime.UtcNow
        });

        _dbContext.UserRecoveryCodes.AddRange(recoveryEntities);
        await _dbContext.SaveChangesAsync();
        await _auditLogger.LogAsync("auth.recovery_regenerated", user.Id, tenantId.Value, "User", user.Id);

        return Ok(recoveryCodes);
    }
}
