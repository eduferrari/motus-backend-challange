using Ambev.DeveloperEvaluation.Application.Sales;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public class CancelSaleItemCommand : IRequest<SaleResult>
{
    public Guid SaleId { get; set; }
    public Guid ItemId { get; set; }
}
