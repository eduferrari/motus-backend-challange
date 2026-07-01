using Ambev.DeveloperEvaluation.Domain.ReadModel;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Sales;

public class MongoSaleReadRepositoryTests : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder()
        .WithImage("mongo:8.0")
        .Build();

    private MongoSaleReadRepository _repository = null!;

    public async Task InitializeAsync()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        await _container.StartAsync();
        var client = new MongoClient(_container.GetConnectionString());
        var database = client.GetDatabase("test_read_model");
        _repository = new MongoSaleReadRepository(database);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact(DisplayName = "Given upserted document When getting by id Then returns correct document")]
    public async Task UpsertAsync_ThenGetById_ReturnsDocument()
    {
        var document = BuildDocument("S-MONGO-001", Guid.NewGuid(), Guid.NewGuid());

        await _repository.UpsertAsync(document);
        var result = await _repository.GetByIdAsync(document.Id);

        result.Should().NotBeNull();
        result!.SaleNumber.Should().Be("S-MONGO-001");
        result.TotalAmount.Should().Be(document.TotalAmount);
        result.Items.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Given upserted document When upserting again Then updates existing document")]
    public async Task UpsertAsync_ExistingDocument_UpdatesInPlace()
    {
        var document = BuildDocument("S-MONGO-UPD", Guid.NewGuid(), Guid.NewGuid());
        await _repository.UpsertAsync(document);

        document.TotalAmount = 9999m;
        document.IsCancelled = true;
        await _repository.UpsertAsync(document);

        var result = await _repository.GetByIdAsync(document.Id);
        result!.TotalAmount.Should().Be(9999m);
        result.IsCancelled.Should().BeTrue();
    }

    [Fact(DisplayName = "Given documents When searching with customerId filter Then returns only matching documents")]
    public async Task SearchAsync_CustomerIdFilter_ReturnsOnlyMatchingDocuments()
    {
        var targetCustomer = Guid.NewGuid();
        var otherCustomer = Guid.NewGuid();

        await _repository.UpsertAsync(BuildDocument("S-C1-001", targetCustomer, Guid.NewGuid()));
        await _repository.UpsertAsync(BuildDocument("S-C1-002", targetCustomer, Guid.NewGuid()));
        await _repository.UpsertAsync(BuildDocument("S-C2-001", otherCustomer, Guid.NewGuid()));

        var (items, total) = await _repository.SearchAsync(page: 1, size: 10, customerId: targetCustomer);

        total.Should().Be(2);
        items.Should().AllSatisfy(i => i.CustomerId.Should().Be(targetCustomer));
    }

    [Fact(DisplayName = "Given documents When searching with isCancelled filter Then returns only matching documents")]
    public async Task SearchAsync_IsCancelledFilter_ReturnsOnlyMatchingDocuments()
    {
        var branchId = Guid.NewGuid();
        var cancelled = BuildDocument("S-FILT-CANCEL", Guid.NewGuid(), branchId);
        cancelled.IsCancelled = true;
        var active = BuildDocument("S-FILT-ACTIVE", Guid.NewGuid(), branchId);

        await _repository.UpsertAsync(cancelled);
        await _repository.UpsertAsync(active);

        var (items, total) = await _repository.SearchAsync(page: 1, size: 10, branchId: branchId, isCancelled: false);

        total.Should().Be(1);
        items.Single().SaleNumber.Should().Be("S-FILT-ACTIVE");
    }

    [Fact(DisplayName = "Given documents When searching with pagination Then returns correct page")]
    public async Task SearchAsync_Pagination_ReturnsCorrectPage()
    {
        var customerId = Guid.NewGuid();
        for (var i = 1; i <= 5; i++)
            await _repository.UpsertAsync(BuildDocument($"S-PAGE-{i:D3}", customerId, Guid.NewGuid()));

        var (items, total) = await _repository.SearchAsync(
            page: 2, size: 2, customerId: customerId, order: "saleNumber asc");

        total.Should().Be(5);
        items.Should().HaveCount(2);
        items.First().SaleNumber.Should().Be("S-PAGE-003");
    }

    [Fact(DisplayName = "Given documents When searching ordered by totalAmount desc Then returns correct order")]
    public async Task SearchAsync_OrderByTotalAmountDesc_ReturnsCorrectOrder()
    {
        var customerId = Guid.NewGuid();
        var low = BuildDocument("S-ORD-LOW", customerId, Guid.NewGuid(), totalAmount: 100m);
        var high = BuildDocument("S-ORD-HIGH", customerId, Guid.NewGuid(), totalAmount: 500m);
        var mid = BuildDocument("S-ORD-MID", customerId, Guid.NewGuid(), totalAmount: 300m);

        await _repository.UpsertAsync(low);
        await _repository.UpsertAsync(high);
        await _repository.UpsertAsync(mid);

        var (items, _) = await _repository.SearchAsync(page: 1, size: 10, customerId: customerId, order: "totalAmount desc");

        items.Select(i => i.SaleNumber).Should().ContainInOrder("S-ORD-HIGH", "S-ORD-MID", "S-ORD-LOW");
    }

    [Fact(DisplayName = "Given document When deleting Then document is removed")]
    public async Task DeleteAsync_ExistingDocument_RemovesFromCollection()
    {
        var document = BuildDocument("S-DEL-001", Guid.NewGuid(), Guid.NewGuid());
        await _repository.UpsertAsync(document);

        var deleted = await _repository.DeleteAsync(document.Id);
        var result = await _repository.GetByIdAsync(document.Id);

        deleted.Should().BeTrue();
        result.Should().BeNull();
    }

    [Fact(DisplayName = "Given non-existent id When deleting Then returns false")]
    public async Task DeleteAsync_NonExistentDocument_ReturnsFalse()
    {
        var deleted = await _repository.DeleteAsync(Guid.NewGuid());
        deleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Given documents When searching by saleNumber wildcard Then returns matching documents")]
    public async Task SearchAsync_SaleNumberWildcard_ReturnsMatchingDocuments()
    {
        var customerId = Guid.NewGuid();
        await _repository.UpsertAsync(BuildDocument("SALE-2024-001", customerId, Guid.NewGuid()));
        await _repository.UpsertAsync(BuildDocument("SALE-2024-002", customerId, Guid.NewGuid()));
        await _repository.UpsertAsync(BuildDocument("ORDER-2024-001", customerId, Guid.NewGuid()));

        var (items, total) = await _repository.SearchAsync(
            page: 1, size: 10, customerId: customerId, saleNumber: "SALE*");

        total.Should().Be(2);
        items.Should().AllSatisfy(i => i.SaleNumber.Should().StartWith("SALE"));
    }

    // ─── Helper ─────────────────────────────────────────────────────────────

    private static SaleDocument BuildDocument(
        string saleNumber, Guid customerId, Guid branchId, decimal totalAmount = 440m) => new()
    {
        Id = Guid.NewGuid(),
        SaleNumber = saleNumber,
        SaleDate = DateTime.UtcNow,
        CustomerId = customerId,
        CustomerName = "Customer",
        BranchId = branchId,
        BranchName = "Branch",
        TotalAmount = totalAmount,
        IsCancelled = false,
        CreatedAt = DateTime.UtcNow,
        Items =
        [
            new SaleItemDocument { Id = Guid.NewGuid(), ProductName = "P1", Quantity = 4, UnitPrice = 110m, TotalAmount = 440m },
            new SaleItemDocument { Id = Guid.NewGuid(), ProductName = "P2", Quantity = 2, UnitPrice = 0m, TotalAmount = 0m }
        ]
    };
}
