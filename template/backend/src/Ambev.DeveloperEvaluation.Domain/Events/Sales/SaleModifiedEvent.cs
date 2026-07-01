namespace Ambev.DeveloperEvaluation.Domain.Events.Sales;

public class SaleModifiedEvent
{
    public Guid SaleId { get; init; }
    public string SaleNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime SaleDate { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
