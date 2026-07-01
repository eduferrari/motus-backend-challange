using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Events.Sales;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public class UpdateSaleHandler : IRequestHandler<UpdateSaleCommand, SaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateSaleHandler> _logger;
    private readonly IDomainEventPublisher _eventPublisher;

    public UpdateSaleHandler(
        ISaleRepository saleRepository,
        IMapper mapper,
        ILogger<UpdateSaleHandler> logger,
        IDomainEventPublisher eventPublisher)
    {
        _saleRepository = saleRepository;
        _mapper = mapper;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task<SaleResult> Handle(UpdateSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sale is null)
            throw new KeyNotFoundException($"Sale with ID {request.Id} not found");

        var existingSale = await _saleRepository.GetBySaleNumberAsync(request.SaleNumber, cancellationToken);
        if (existingSale is not null && existingSale.Id != request.Id)
            throw new InvalidOperationException($"Sale with number {request.SaleNumber} already exists");

        sale.Update(
            request.SaleNumber,
            request.SaleDate,
            request.CustomerId,
            request.CustomerName,
            request.BranchId,
            request.BranchName,
            request.Items.Select(i => new SaleItem(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)));

        var updatedSale = await _saleRepository.UpdateAsync(sale, cancellationToken);

        _logger.LogInformation("SaleModified: {SaleId}", updatedSale.Id);

        await _eventPublisher.PublishAsync(new SaleModifiedEvent
        {
            SaleId = updatedSale.Id,
            SaleNumber = updatedSale.SaleNumber,
            CustomerId = updatedSale.CustomerId,
            CustomerName = updatedSale.CustomerName,
            BranchId = updatedSale.BranchId,
            BranchName = updatedSale.BranchName,
            TotalAmount = updatedSale.TotalAmount,
            SaleDate = updatedSale.SaleDate
        }, cancellationToken);

        return _mapper.Map<SaleResult>(updatedSale);
    }
}
