using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Events.Sales;
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
    private readonly IDomainEventPublisher _eventPublisher;

    public CreateSaleHandler(
        ISaleRepository saleRepository,
        IMapper mapper,
        ILogger<CreateSaleHandler> logger,
        IDomainEventPublisher eventPublisher)
    {
        _saleRepository = saleRepository;
        _mapper = mapper;
        _logger = logger;
        _eventPublisher = eventPublisher;
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

        await _eventPublisher.PublishAsync(new SaleCreatedEvent
        {
            SaleId = createdSale.Id,
            SaleNumber = createdSale.SaleNumber,
            CustomerId = createdSale.CustomerId,
            CustomerName = createdSale.CustomerName,
            BranchId = createdSale.BranchId,
            BranchName = createdSale.BranchName,
            TotalAmount = createdSale.TotalAmount,
            SaleDate = createdSale.SaleDate
        }, cancellationToken);

        return _mapper.Map<SaleResult>(createdSale);
    }
}
