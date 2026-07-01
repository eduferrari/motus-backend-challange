using Ambev.DeveloperEvaluation.Domain.Common;

namespace Ambev.DeveloperEvaluation.Domain.Entities;

public class Sale : BaseEntity
{
    private readonly List<SaleItem> _items = [];

    public string SaleNumber { get; private set; } = string.Empty;
    public DateTime SaleDate { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public Guid BranchId { get; private set; }
    public string BranchName { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public bool IsCancelled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();

    private Sale()
    {
    }

    public Sale(
        string saleNumber,
        DateTime saleDate,
        Guid customerId,
        string customerName,
        Guid branchId,
        string branchName,
        IEnumerable<SaleItem> items)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        ApplySaleData(saleNumber, saleDate, customerId, customerName, branchId, branchName, items);
    }

    public void Update(
        string saleNumber,
        DateTime saleDate,
        Guid customerId,
        string customerName,
        Guid branchId,
        string branchName,
        IEnumerable<SaleItem> items)
    {
        if (IsCancelled)
            throw new InvalidOperationException("Cancelled sales cannot be modified");

        ApplySaleData(saleNumber, saleDate, customerId, customerName, branchId, branchName, items);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (IsCancelled)
            return;

        IsCancelled = true;
        foreach (var item in _items)
            item.Cancel();

        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;
    }

    public void CancelItem(Guid itemId)
    {
        if (IsCancelled)
            throw new InvalidOperationException("Items from cancelled sales cannot be modified");

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            throw new KeyNotFoundException($"Sale item with ID {itemId} not found");

        item.Cancel();
        RecalculateTotal();

        if (_items.All(i => i.IsCancelled))
        {
            IsCancelled = true;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    private void ApplySaleData(
        string saleNumber,
        DateTime saleDate,
        Guid customerId,
        string customerName,
        Guid branchId,
        string branchName,
        IEnumerable<SaleItem> items)
    {
        if (string.IsNullOrWhiteSpace(saleNumber))
            throw new ArgumentException("Sale number is required", nameof(saleNumber));

        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer id is required", nameof(customerId));

        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name is required", nameof(customerName));

        if (branchId == Guid.Empty)
            throw new ArgumentException("Branch id is required", nameof(branchId));

        if (string.IsNullOrWhiteSpace(branchName))
            throw new ArgumentException("Branch name is required", nameof(branchName));

        var saleItems = items.ToList();
        if (saleItems.Count == 0)
            throw new ArgumentException("A sale must have at least one item", nameof(items));

        SaleNumber = saleNumber.Trim();
        SaleDate = saleDate;
        CustomerId = customerId;
        CustomerName = customerName.Trim();
        BranchId = branchId;
        BranchName = branchName.Trim();

        _items.Clear();
        _items.AddRange(saleItems);
        RecalculateTotal();
    }

    private void RecalculateTotal()
    {
        TotalAmount = IsCancelled ? 0 : _items.Where(i => !i.IsCancelled).Sum(i => i.TotalAmount);
    }
}
