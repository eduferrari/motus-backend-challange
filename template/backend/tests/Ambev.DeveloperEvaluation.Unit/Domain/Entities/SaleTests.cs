using Ambev.DeveloperEvaluation.Domain.Entities;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Entities;

public class SaleTests
{
    [Theory(DisplayName = "Sale item discount should follow quantity tiers")]
    [InlineData(3, 0)]
    [InlineData(4, 40)]
    [InlineData(9, 90)]
    [InlineData(10, 200)]
    [InlineData(20, 400)]
    public void Given_Quantity_When_CreatingSaleItem_Then_DiscountShouldFollowTier(int quantity, decimal expectedDiscount)
    {
        var item = new SaleItem(Guid.NewGuid(), "Product", quantity, 100);

        Assert.Equal(expectedDiscount, item.Discount);
        Assert.Equal(quantity * 100 - expectedDiscount, item.TotalAmount);
    }

    [Fact(DisplayName = "Sale item should reject quantities above 20")]
    public void Given_QuantityAboveTwenty_When_CreatingSaleItem_Then_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new SaleItem(Guid.NewGuid(), "Product", 21, 100));
    }

    [Fact(DisplayName = "Sale total should ignore cancelled items")]
    public void Given_Sale_When_ItemIsCancelled_Then_TotalShouldBeRecalculated()
    {
        var firstItem = new SaleItem(Guid.NewGuid(), "First product", 4, 100);
        var secondItem = new SaleItem(Guid.NewGuid(), "Second product", 2, 100);
        var sale = CreateSale(firstItem, secondItem);

        sale.CancelItem(firstItem.Id);

        Assert.True(firstItem.IsCancelled);
        Assert.Equal(0, firstItem.TotalAmount);
        Assert.Equal(200, sale.TotalAmount);
    }

    [Fact(DisplayName = "Sale cancellation should cancel all items and zero total")]
    public void Given_Sale_When_Cancelled_Then_AllItemsShouldBeCancelled()
    {
        var sale = CreateSale(
            new SaleItem(Guid.NewGuid(), "First product", 4, 100),
            new SaleItem(Guid.NewGuid(), "Second product", 10, 100));

        sale.Cancel();

        Assert.True(sale.IsCancelled);
        Assert.Equal(0, sale.TotalAmount);
        Assert.All(sale.Items, item => Assert.True(item.IsCancelled));
    }

    private static Sale CreateSale(params SaleItem[] items)
    {
        return new Sale(
            "S-001",
            DateTime.UtcNow,
            Guid.NewGuid(),
            "Customer",
            Guid.NewGuid(),
            "Branch",
            items);
    }
}
