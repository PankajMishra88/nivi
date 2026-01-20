using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Domain.Entities;

public class Transaction : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public DateOnly Date { get; set; }
    public Guid? FromAccountId { get; set; }
    public Guid? ToAccountId { get; set; }
    public Guid? LoanId { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string OriginalCurrency { get; set; } = "INR";
    public decimal OriginalAmount { get; set; }
    public decimal? FxRateUsed { get; set; }
    public decimal BaseAmount { get; set; }
    public string? AdjustmentReason { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
