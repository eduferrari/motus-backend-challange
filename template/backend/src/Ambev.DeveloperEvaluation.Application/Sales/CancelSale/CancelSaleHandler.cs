using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Events.Sales;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public class CancelSaleHandler : IRequestHandler<CancelSaleCommand, SaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<CancelSaleHandler> _logger;
    private readonly IDomainEventPublisher _eventPublisher;

    public CancelSaleHandler(
        ISaleRepository saleRepository,
        IMapper mapper,
        ILogger<CancelSaleHandler> logger,
        IDomainEventPublisher eventPublisher)
    {
        _saleRepository = saleRepository;
        _mapper = mapper;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task<SaleResult> Handle(CancelSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sale is null)
            throw new KeyNotFoundException($"Sale with ID {request.Id} not found");

        if (sale.IsCancelled)
            return _mapper.Map<SaleResult>(sale);

        sale.Cancel();
        var cancelledSale = await _saleRepository.UpdateAsync(sale, cancellationToken);

        _logger.LogInformation("SaleCancelled: {SaleId}", cancelledSale.Id);

        await _eventPublisher.PublishAsync(new SaleCancelledEvent
        {
            SaleId = cancelledSale.Id,
            SaleNumber = cancelledSale.SaleNumber,
            CustomerId = cancelledSale.CustomerId,
            CustomerName = cancelledSale.CustomerName
        }, cancellationToken);

        return _mapper.Map<SaleResult>(cancelledSale);
    }
}
