using Ambev.DeveloperEvaluation.Domain.ReadModel;

namespace Ambev.DeveloperEvaluation.Domain.Repositories;

public interface ISaleReadRepository
{
    Task<SaleDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SaleDocument?> GetBySaleNumberAsync(string saleNumber, CancellationToken cancellationToken = default);
    Task<(IReadOnlyCollection<SaleDocument> Items, int TotalCount)> SearchAsync(
        int page,
        int size,
        string? order = null,
        string? saleNumber = null,
        Guid? customerId = null,
        Guid? branchId = null,
        bool? isCancelled = null,
        DateTime? minSaleDate = null,
        DateTime? maxSaleDate = null,
        CancellationToken cancellationToken = default);
    Task UpsertAsync(SaleDocument document, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
