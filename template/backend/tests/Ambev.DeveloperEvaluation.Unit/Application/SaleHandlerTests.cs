using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSales;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public class SaleHandlerTests
{
    private readonly ISaleRepository _saleRepository;
    private readonly IMapper _mapper;
    private readonly IDomainEventPublisher _eventPublisher;

    public SaleHandlerTests()
    {
        _saleRepository = Substitute.For<ISaleRepository>();
        _mapper = Substitute.For<IMapper>();
        _eventPublisher = Substitute.For<IDomainEventPublisher>();
    }

    [Fact(DisplayName = "Given valid sale data When creating sale Then persists sale with calculated totals")]
    public async Task Handle_CreateSaleValidRequest_PersistsSaleWithCalculatedTotals()
    {
        var command = CreateValidCommand();
        var result = new SaleResult { SaleNumber = command.SaleNumber };

        _saleRepository.GetBySaleNumberAsync(command.SaleNumber, Arg.Any<CancellationToken>())
            .Returns((Sale?)null);
        _saleRepository.CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<SaleResult>(Arg.Any<Sale>()).Returns(result);

        var handler = new CreateSaleHandler(
            _saleRepository,
            _mapper,
            NullLogger<CreateSaleHandler>.Instance,
            _eventPublisher);

        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().Be(result);
        await _saleRepository.Received(1).CreateAsync(
            Arg.Is<Sale>(sale =>
                sale.SaleNumber == command.SaleNumber &&
                sale.TotalAmount == 1_160m &&
                sale.Items.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given existing sale number When creating sale Then rejects duplicate")]
    public async Task Handle_CreateSaleDuplicateNumber_ThrowsInvalidOperationException()
    {
        var command = CreateValidCommand();
        var existingSale = new Sale(
            command.SaleNumber,
            DateTime.UtcNow,
            Guid.NewGuid(),
            "Existing customer",
            Guid.NewGuid(),
            "Existing branch",
            [new SaleItem(Guid.NewGuid(), "Existing product", 1, 10)]);

        _saleRepository.GetBySaleNumberAsync(command.SaleNumber, Arg.Any<CancellationToken>())
            .Returns(existingSale);

        var handler = new CreateSaleHandler(
            _saleRepository,
            _mapper,
            NullLogger<CreateSaleHandler>.Instance,
            _eventPublisher);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Sale with number {command.SaleNumber} already exists");
        await _saleRepository.DidNotReceive().CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given sale and item ids When cancelling sale item Then updates sale total")]
    public async Task Handle_CancelSaleItemValidRequest_UpdatesSaleTotal()
    {
        var firstItem = new SaleItem(Guid.NewGuid(), "First product", 4, 100);
        var secondItem = new SaleItem(Guid.NewGuid(), "Second product", 2, 100);
        var sale = new Sale(
            "S-ITEM-CANCEL",
            DateTime.UtcNow,
            Guid.NewGuid(),
            "Customer",
            Guid.NewGuid(),
            "Branch",
            [firstItem, secondItem]);
        var command = new CancelSaleItemCommand
        {
            SaleId = sale.Id,
            ItemId = firstItem.Id
        };
        var result = new SaleResult { Id = sale.Id, TotalAmount = 200 };

        _saleRepository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>())
            .Returns(sale);
        _saleRepository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<SaleResult>(Arg.Any<Sale>()).Returns(result);

        var handler = new CancelSaleItemHandler(
            _saleRepository,
            _mapper,
            NullLogger<CancelSaleItemHandler>.Instance,
            _eventPublisher);

        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().Be(result);
        firstItem.IsCancelled.Should().BeTrue();
        sale.TotalAmount.Should().Be(200);
        await _saleRepository.Received(1).UpdateAsync(sale, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given sales query When listing sales Then returns paginated result")]
    public async Task Handle_GetSalesValidRequest_ReturnsPaginatedResult()
    {
        var sale = new Sale(
            "S-LIST-001",
            DateTime.UtcNow,
            Guid.NewGuid(),
            "Customer",
            Guid.NewGuid(),
            "Branch",
            [new SaleItem(Guid.NewGuid(), "Product", 4, 100)]);
        var command = new GetSalesCommand { Page = 2, Size = 1, Order = "saleNumber asc" };
        var mappedSale = new SaleResult { Id = sale.Id, SaleNumber = sale.SaleNumber };

        _saleRepository.SearchAsync(
                command.Page,
                command.Size,
                command.Order,
                command.SaleNumber,
                command.CustomerId,
                command.BranchId,
                command.IsCancelled,
                command.MinSaleDate,
                command.MaxSaleDate,
                Arg.Any<CancellationToken>())
            .Returns(([sale], 3));
        _mapper.Map<IReadOnlyCollection<SaleResult>>(Arg.Any<IReadOnlyCollection<Sale>>())
            .Returns([mappedSale]);

        var handler = new GetSalesHandler(_saleRepository, _mapper);

        var response = await handler.Handle(command, CancellationToken.None);

        response.Items.Should().ContainSingle().Which.Should().Be(mappedSale);
        response.CurrentPage.Should().Be(2);
        response.TotalPages.Should().Be(3);
        response.TotalCount.Should().Be(3);
    }

    private static CreateSaleCommand CreateValidCommand()
    {
        return new CreateSaleCommand
        {
            SaleNumber = "S-APP-001",
            SaleDate = DateTime.UtcNow,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Customer",
            BranchId = Guid.NewGuid(),
            BranchName = "Branch",
            Items =
            [
                new SaleItemInput
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Discounted product",
                    Quantity = 10,
                    UnitPrice = 100
                },
                new SaleItemInput
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Regular product",
                    Quantity = 4,
                    UnitPrice = 100
                }
            ]
        };
    }
}
