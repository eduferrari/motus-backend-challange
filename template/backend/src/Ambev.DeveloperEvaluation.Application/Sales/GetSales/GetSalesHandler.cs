using Ambev.DeveloperEvaluation.Application.Common;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSales;

public class GetSalesHandler : IRequestHandler<GetSalesCommand, PagedResult<SaleResult>>
{
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IMapper _mapper;

    public GetSalesHandler(ISaleReadRepository saleReadRepository, IMapper mapper)
    {
        _saleReadRepository = saleReadRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<SaleResult>> Handle(GetSalesCommand request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _saleReadRepository.SearchAsync(
            request.Page,
            request.Size,
            request.Order,
            request.SaleNumber,
            request.CustomerId,
            request.BranchId,
            request.IsCancelled,
            request.MinSaleDate,
            request.MaxSaleDate,
            cancellationToken);

        return new PagedResult<SaleResult>
        {
            Items = _mapper.Map<IReadOnlyCollection<SaleResult>>(items),
            CurrentPage = request.Page,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.Size),
            TotalCount = totalCount
        };
    }
}
