using Microsoft.EntityFrameworkCore;
using Nivi.WealthOS.Api.Domain.Entities;
using Nivi.WealthOS.Api.Domain.Enums;
using Nivi.WealthOS.Api.Services;

namespace Nivi.WealthOS.Api.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public Guid? TenantId => _tenantContext.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<WealthEntity> Entities => Set<WealthEntity>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<FxRate> FxRates => Set<FxRate>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            entity.Property(x => x.BaseCurrency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Timezone).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(120);
            entity.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ScopeType).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => new { x.TenantId, x.UserId });
            entity.HasIndex(x => new { x.TenantId, x.ScopeType, x.ScopeId });
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Name });
        });

        modelBuilder.Entity<WealthEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => new { x.TenantId, x.GroupId });
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.OpeningBalance).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.EntityId });
            entity.HasIndex(x => new { x.TenantId, x.Currency });
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.OriginalCurrency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.OriginalAmount).HasPrecision(18, 2);
            entity.Property(x => x.BaseAmount).HasPrecision(18, 2);
            entity.Property(x => x.FxRateUsed).HasPrecision(18, 6);
            entity.Property(x => x.AdjustmentReason).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.Date });
            entity.HasIndex(x => new { x.TenantId, x.EntityId });
            entity.HasIndex(x => new { x.TenantId, x.Type });
            entity.HasIndex(x => new { x.TenantId, x.OriginalCurrency });
            entity.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<FxRate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BaseCurrency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.QuoteCurrency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Rate).HasPrecision(18, 6);
            entity.HasIndex(x => new { x.TenantId, x.Date, x.BaseCurrency, x.QuoteCurrency }).IsUnique();
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LinkedObjectType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.LinkedObjectType, x.LinkedObjectId });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(120);
            entity.Property(x => x.IpAddress).HasMaxLength(100);
            entity.Property(x => x.UserAgent).HasMaxLength(300);
            entity.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.JwtId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(100);
            entity.Property(x => x.UserAgent).HasMaxLength(300);
            entity.HasIndex(x => new { x.TenantId, x.UserId });
            entity.HasIndex(x => new { x.TenantId, x.JwtId }).IsUnique();
        });

        modelBuilder.Entity<UserRecoveryCode>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CodeHash).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.UserId });
        });

        ApplyTenantFilters(modelBuilder);
    }

    public override int SaveChanges()
    {
        ApplyTenantRules();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantRules();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTenantRules()
    {
        var tenantId = _tenantContext.TenantId;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAtUtc") != null)
            {
                entry.Property("CreatedAtUtc").CurrentValue = DateTime.UtcNow;
            }

            if (entry.Entity is ITenantOwned tenantOwned)
            {
                if (!tenantId.HasValue)
                {
                    throw new InvalidOperationException("Tenant context is required for tenant-owned data.");
                }

                if (entry.State == EntityState.Added && tenantOwned.TenantId == Guid.Empty)
                {
                    tenantOwned.TenantId = tenantId.Value;
                }

                if (tenantOwned.TenantId != tenantId.Value)
                {
                    throw new InvalidOperationException("Cross-tenant access blocked.");
                }
            }
        }
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        var tenantOwnedTypes = modelBuilder.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType));

        foreach (var entityType in tenantOwnedTypes)
        {
            var method = typeof(AppDbContext)
                .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.MakeGenericMethod(entityType.ClrType);

            method?.Invoke(this, new object[] { modelBuilder });
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => TenantId.HasValue && entity.TenantId == TenantId.Value);
    }
}
