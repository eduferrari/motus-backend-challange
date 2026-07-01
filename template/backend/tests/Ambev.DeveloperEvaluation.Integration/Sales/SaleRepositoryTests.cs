using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Sales;

public class SaleRepositoryTests
{
    [Fact(DisplayName = "Given persisted sales When searching Then applies filters ordering and pagination")]
    public async Task SearchAsync_ShouldApplyFiltersOrderingAndPagination()
    {
        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new DefaultContext(options);
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        await context.Sales.AddRangeAsync(
            CreateSale("S-001", customerId, branchId, 4, 100),
            CreateSale("S-002", customerId, branchId, 10, 100),
            CreateSale("S-003", Guid.NewGuid(), branchId, 20, 100));
        await context.SaveChangesAsync();

        var repository = new SaleRepository(context);

        var (items, totalCount) = await repository.SearchAsync(
            page: 1,
            size: 1,
            order: "totalAmount desc",
            saleNumber: null,
            customerId: customerId,
            branchId: null,
            isCancelled: false,
            minSaleDate: null,
            maxSaleDate: null);

        Assert.Equal(2, totalCount);
        var sale = Assert.Single(items);
        Assert.Equal("S-002", sale.SaleNumber);
        Assert.Equal(800m, sale.TotalAmount);
        Assert.NotEmpty(sale.Items);
    }

    private static Sale CreateSale(string saleNumber, Guid customerId, Guid branchId, int quantity, decimal unitPrice)
    {
        return new Sale(
            saleNumber,
            DateTime.UtcNow,
            customerId,
            "Customer",
            branchId,
            "Branch",
            [new SaleItem(Guid.NewGuid(), $"Product {saleNumber}", quantity, unitPrice)]);
    }
}
