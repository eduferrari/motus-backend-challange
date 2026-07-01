namespace Ambev.DeveloperEvaluation.Domain.Events.Sales;

public class SaleCancelledEvent
{
    public Guid SaleId { get; init; }
    public string SaleNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
