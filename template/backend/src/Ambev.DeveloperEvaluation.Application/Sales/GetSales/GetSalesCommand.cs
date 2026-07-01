using Ambev.DeveloperEvaluation.Application.Common;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSales;

public class GetSalesCommand : IRequest<PagedResult<SaleResult>>
{
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 10;
    public string? Order { get; set; }
    public string? SaleNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? BranchId { get; set; }
    public bool? IsCancelled { get; set; }
    public DateTime? MinSaleDate { get; set; }
    public DateTime? MaxSaleDate { get; set; }
}
