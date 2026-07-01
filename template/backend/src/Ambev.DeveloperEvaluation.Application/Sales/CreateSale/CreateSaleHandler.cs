using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public class CreateSaleHandler : IRequestHandler<CreateSaleCommand, SaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateSaleHandler> _logger;

    public CreateSaleHandler(
        ISaleRepository saleRepository,
        IMapper mapper,
        ILogger<CreateSaleHandler> logger)
    {
        _saleRepository = saleRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SaleResult> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var existingSale = await _saleRepository.GetBySaleNumberAsync(request.SaleNumber, cancellationToken);
        if (existingSale is not null)
            throw new InvalidOperationException($"Sale with number {request.SaleNumber} already exists");

        var sale = new Sale(
            request.SaleNumber,
            request.SaleDate,
            request.CustomerId,
            request.CustomerName,
            request.BranchId,
            request.BranchName,
            request.Items.Select(i => new SaleItem(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)));

        var createdSale = await _saleRepository.CreateAsync(sale, cancellationToken);
        _logger.LogInformation("SaleCreated: {SaleId}", createdSale.Id);

        return _mapper.Map<SaleResult>(createdSale);
    }
}
