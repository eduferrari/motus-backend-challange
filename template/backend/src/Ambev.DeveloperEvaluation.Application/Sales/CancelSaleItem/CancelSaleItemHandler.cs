using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Events.Sales;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public class CancelSaleItemHandler : IRequestHandler<CancelSaleItemCommand, SaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<CancelSaleItemHandler> _logger;
    private readonly IDomainEventPublisher _eventPublisher;

    public CancelSaleItemHandler(
        ISaleRepository saleRepository,
        IMapper mapper,
        ILogger<CancelSaleItemHandler> logger,
        IDomainEventPublisher eventPublisher)
    {
        _saleRepository = saleRepository;
        _mapper = mapper;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task<SaleResult> Handle(CancelSaleItemCommand request, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdAsync(request.SaleId, cancellationToken);
        if (sale is null)
            throw new KeyNotFoundException($"Sale with ID {request.SaleId} not found");

        var item = sale.Items.FirstOrDefault(i => i.Id == request.ItemId)
            ?? throw new KeyNotFoundException($"Item with ID {request.ItemId} not found in sale {request.SaleId}");

        sale.CancelItem(request.ItemId);
        var updatedSale = await _saleRepository.UpdateAsync(sale, cancellationToken);

        _logger.LogInformation("ItemCancelled: {SaleItemId}", request.ItemId);

        await _eventPublisher.PublishAsync(new ItemCancelledEvent
        {
            SaleId = updatedSale.Id,
            SaleNumber = updatedSale.SaleNumber,
            ItemId = item.Id,
            ProductId = item.ProductId,
            ProductName = item.ProductName
        }, cancellationToken);

        return _mapper.Map<SaleResult>(updatedSale);
    }
}
