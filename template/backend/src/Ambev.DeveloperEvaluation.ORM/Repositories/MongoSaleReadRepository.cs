using Ambev.DeveloperEvaluation.Domain.ReadModel;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using MongoDB.Driver;

namespace Ambev.DeveloperEvaluation.ORM.Repositories;

public class MongoSaleReadRepository : ISaleReadRepository
{
    private readonly IMongoCollection<SaleDocument> _collection;

    public MongoSaleReadRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SaleDocument>("sales");
    }

    public async Task<SaleDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _collection.Find(s => s.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<SaleDocument?> GetBySaleNumberAsync(string saleNumber, CancellationToken cancellationToken = default)
        => await _collection.Find(s => s.SaleNumber == saleNumber).FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyCollection<SaleDocument> Items, int TotalCount)> SearchAsync(
        int page,
        int size,
        string? order = null,
        string? saleNumber = null,
        Guid? customerId = null,
        Guid? branchId = null,
        bool? isCancelled = null,
        DateTime? minSaleDate = null,
        DateTime? maxSaleDate = null,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(saleNumber, customerId, branchId, isCancelled, minSaleDate, maxSaleDate);
        var totalCount = (int)await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var sort = BuildSort(order);
        var items = await _collection
            .Find(filter)
            .Sort(sort)
            .Skip((page - 1) * size)
            .Limit(size)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task UpsertAsync(SaleDocument document, CancellationToken cancellationToken = default)
    {
        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(s => s.Id == document.Id, document, options, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _collection.DeleteOneAsync(s => s.Id == id, cancellationToken);
        return result.DeletedCount > 0;
    }

    private static FilterDefinition<SaleDocument> BuildFilter(
        string? saleNumber, Guid? customerId, Guid? branchId,
        bool? isCancelled, DateTime? minSaleDate, DateTime? maxSaleDate)
    {
        var builder = Builders<SaleDocument>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(saleNumber))
            filter &= builder.Regex(s => s.SaleNumber,
                new MongoDB.Bson.BsonRegularExpression(saleNumber.Replace("*", ".*"), "i"));

        if (customerId.HasValue)
            filter &= builder.Eq(s => s.CustomerId, customerId.Value);

        if (branchId.HasValue)
            filter &= builder.Eq(s => s.BranchId, branchId.Value);

        if (isCancelled.HasValue)
            filter &= builder.Eq(s => s.IsCancelled, isCancelled.Value);

        if (minSaleDate.HasValue)
            filter &= builder.Gte(s => s.SaleDate, minSaleDate.Value);

        if (maxSaleDate.HasValue)
            filter &= builder.Lte(s => s.SaleDate, maxSaleDate.Value);

        return filter;
    }

    private static SortDefinition<SaleDocument> BuildSort(string? order)
    {
        var builder = Builders<SaleDocument>.Sort;

        if (string.IsNullOrWhiteSpace(order))
            return builder.Descending(s => s.SaleDate);

        var parts = order.Trim().Split(' ');
        var field = parts[0].ToLowerInvariant();
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "salenumber" => descending ? builder.Descending(s => s.SaleNumber) : builder.Ascending(s => s.SaleNumber),
            "customername" => descending ? builder.Descending(s => s.CustomerName) : builder.Ascending(s => s.CustomerName),
            "branchname" => descending ? builder.Descending(s => s.BranchName) : builder.Ascending(s => s.BranchName),
            "totalamount" => descending ? builder.Descending(s => s.TotalAmount) : builder.Ascending(s => s.TotalAmount),
            "iscancelled" => descending ? builder.Descending(s => s.IsCancelled) : builder.Ascending(s => s.IsCancelled),
            _ => descending ? builder.Descending(s => s.SaleDate) : builder.Ascending(s => s.SaleDate)
        };
    }
}
