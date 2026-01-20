using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Domain.Entities;

public class Loan : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public LoanDirection Direction { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid LenderUserId { get; set; }
    public string? BorrowerName { get; set; }
    public string OriginalCurrency { get; set; } = "INR";
    public decimal OriginalAmount { get; set; }
    public decimal? FxRateUsed { get; set; }
    public decimal BaseAmount { get; set; }
    public DateOnly? NextDueDate { get; set; }
    public decimal? EmiAmount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
