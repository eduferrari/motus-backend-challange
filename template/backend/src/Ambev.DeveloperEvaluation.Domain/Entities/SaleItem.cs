using Ambev.DeveloperEvaluation.Domain.Common;

namespace Ambev.DeveloperEvaluation.Domain.Entities;

public class SaleItem : BaseEntity
{
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Discount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public bool IsCancelled { get; private set; }

    private SaleItem()
    {
    }

    public SaleItem(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id is required", nameof(productId));

        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name is required", nameof(productName));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));

        if (quantity > 20)
            throw new InvalidOperationException("It is not possible to sell more than 20 identical items");

        if (unitPrice <= 0)
            throw new ArgumentException("Unit price must be greater than zero", nameof(unitPrice));

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        Discount = CalculateDiscount(quantity, unitPrice);
        TotalAmount = CalculateTotalAmount(quantity, unitPrice, Discount);
    }

    public void Cancel()
    {
        IsCancelled = true;
        TotalAmount = 0;
    }

    private static decimal CalculateDiscount(int quantity, decimal unitPrice)
    {
        var grossAmount = quantity * unitPrice;

        if (quantity >= 10)
            return grossAmount * 0.20m;

        if (quantity >= 4)
            return grossAmount * 0.10m;

        return 0;
    }

    private static decimal CalculateTotalAmount(int quantity, decimal unitPrice, decimal discount)
    {
        return quantity * unitPrice - discount;
    }
}
