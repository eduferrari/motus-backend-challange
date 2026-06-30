using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using FluentValidation;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public class CreateSaleHandler : IRequestHandler<CreateSaleCommand, SaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IMapper _mapper;

    public CreateSaleHandler(ISaleRepository saleRepository, IMapper mapper)
    {
        _saleRepository = saleRepository;
        _mapper = mapper;
    }

    public async Task<SaleResult> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var validator = new CreateSaleValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

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
        Console.WriteLine($"SaleCreated: {createdSale.Id}");

        return _mapper.Map<SaleResult>(createdSale);
    }
}
