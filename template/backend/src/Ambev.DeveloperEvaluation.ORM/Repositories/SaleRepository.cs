using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Ambev.DeveloperEvaluation.ORM.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly DefaultContext _context;

    public SaleRepository(DefaultContext context)
    {
        _context = context;
    }

    public async Task<Sale> CreateAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        await _context.Sales.AddAsync(sale, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return sale;
    }

    public async Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Sale?> GetBySaleNumberAsync(string saleNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SaleNumber == saleNumber, cancellationToken);
    }

    public async Task<(IReadOnlyCollection<Sale> Items, int TotalCount)> SearchAsync(
        int page,
        int size,
        string? order,
        string? saleNumber,
        Guid? customerId,
        Guid? branchId,
        bool? isCancelled,
        DateTime? minSaleDate,
        DateTime? maxSaleDate,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales
            .Include(s => s.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(saleNumber))
        {
            var normalizedSaleNumber = saleNumber.Trim();
            query = normalizedSaleNumber.Contains('*')
                ? query.Where(s => EF.Functions.ILike(s.SaleNumber, normalizedSaleNumber.Replace('*', '%')))
                : query.Where(s => s.SaleNumber == normalizedSaleNumber);
        }

        if (customerId.HasValue)
            query = query.Where(s => s.CustomerId == customerId.Value);

        if (branchId.HasValue)
            query = query.Where(s => s.BranchId == branchId.Value);

        if (isCancelled.HasValue)
            query = query.Where(s => s.IsCancelled == isCancelled.Value);

        if (minSaleDate.HasValue)
            query = query.Where(s => s.SaleDate >= minSaleDate.Value);

        if (maxSaleDate.HasValue)
            query = query.Where(s => s.SaleDate <= maxSaleDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await ApplyOrdering(query, order)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Sale> UpdateAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        var oldItems = await _context.SaleItems
            .Where(i => i.SaleId == sale.Id)
            .ToListAsync(cancellationToken);
        _context.SaleItems.RemoveRange(oldItems);

        _context.Sales.Update(sale);
        await _context.SaveChangesAsync(cancellationToken);
        return sale;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await GetByIdAsync(id, cancellationToken);
        if (sale is null)
            return false;

        _context.Sales.Remove(sale);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static IQueryable<Sale> ApplyOrdering(IQueryable<Sale> query, string? order)
    {
        var orderParts = string.IsNullOrWhiteSpace(order)
            ? ["saleDate desc"]
            : order.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IOrderedQueryable<Sale>? orderedQuery = null;

        foreach (var part in orderParts)
        {
            var tokens = part.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var field = tokens[0];
            var descending = tokens.Length > 1 && tokens[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

            orderedQuery = ApplyOrderingPart(orderedQuery ?? query, field, descending, orderedQuery is not null);
        }

        return orderedQuery ?? query.OrderByDescending(s => s.SaleDate);
    }

    private static IOrderedQueryable<Sale> ApplyOrderingPart(
        IQueryable<Sale> query,
        string field,
        bool descending,
        bool thenBy)
    {
        return field.ToLowerInvariant() switch
        {
            "salenumber" => OrderBy(query, s => s.SaleNumber, descending, thenBy),
            "saledate" => OrderBy(query, s => s.SaleDate, descending, thenBy),
            "customername" => OrderBy(query, s => s.CustomerName, descending, thenBy),
            "branchname" => OrderBy(query, s => s.BranchName, descending, thenBy),
            "totalamount" => OrderBy(query, s => s.TotalAmount, descending, thenBy),
            "iscancelled" => OrderBy(query, s => s.IsCancelled, descending, thenBy),
            _ => OrderBy(query, s => s.SaleDate, descending, thenBy)
        };
    }

    private static IOrderedQueryable<Sale> OrderBy<TKey>(
        IQueryable<Sale> query,
        System.Linq.Expressions.Expression<Func<Sale, TKey>> keySelector,
        bool descending,
        bool thenBy)
    {
        if (thenBy && query is IOrderedQueryable<Sale> orderedQuery)
            return descending ? orderedQuery.ThenByDescending(keySelector) : orderedQuery.ThenBy(keySelector);

        return descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
    }
}
