using Ambev.DeveloperEvaluation.Application.Sales;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSale;

public class GetSaleCommand : IRequest<SaleResult>
{
    public Guid Id { get; set; }
}
