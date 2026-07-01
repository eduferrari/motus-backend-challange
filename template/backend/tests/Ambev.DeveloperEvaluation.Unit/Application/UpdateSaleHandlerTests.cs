using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Events.Sales;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public class UpdateSaleHandlerTests
{
    private readonly ISaleRepository _saleRepository;
    private readonly IMapper _mapper;
    private readonly IDomainEventPublisher _eventPublisher;

    public UpdateSaleHandlerTests()
    {
        _saleRepository = Substitute.For<ISaleRepository>();
        _mapper = Substitute.For<IMapper>();
        _eventPublisher = Substitute.For<IDomainEventPublisher>();
    }

    [Fact(DisplayName = "Given valid update command When handling Then persists changes and publishes SaleModifiedEvent")]
    public async Task Handle_ValidCommand_UpdatesSaleAndPublishesEvent()
    {
        var sale = CreateSale("S-UPD-001");
        var result = new SaleResult { Id = sale.Id, SaleNumber = "S-UPD-001-V2" };
        var command = BuildCommand(sale.Id, "S-UPD-001-V2");

        _saleRepository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _saleRepository.GetBySaleNumberAsync("S-UPD-001-V2", Arg.Any<CancellationToken>()).Returns((Sale?)null);
        _saleRepository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<SaleResult>(Arg.Any<Sale>()).Returns(result);

        var response = await CreateHandler().Handle(command, CancellationToken.None);

        response.Should().Be(result);
        await _saleRepository.Received(1).UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<SaleModifiedEvent>(e => e.SaleId == sale.Id && e.SaleNumber == "S-UPD-001-V2"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given non-existent sale id When handling Then throws KeyNotFoundException")]
    public async Task Handle_SaleNotFound_ThrowsKeyNotFoundException()
    {
        _saleRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Sale?)null);

        var act = () => CreateHandler().Handle(BuildCommand(Guid.NewGuid(), "S-X"), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*not found*");
    }

    [Fact(DisplayName = "Given duplicate sale number on a different sale When handling Then throws InvalidOperationException")]
    public async Task Handle_DuplicateSaleNumber_ThrowsInvalidOperationException()
    {
        var sale = CreateSale("S-UPD-002");
        var other = CreateSale("S-UPD-003");

        _saleRepository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _saleRepository.GetBySaleNumberAsync("S-UPD-003", Arg.Any<CancellationToken>()).Returns(other);

        var command = BuildCommand(sale.Id, "S-UPD-003");
        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
        await _eventPublisher.DidNotReceive().PublishAsync(Arg.Any<SaleModifiedEvent>(), Arg.Any<CancellationToken>());
    }

    private UpdateSaleHandler CreateHandler() =>
        new(_saleRepository, _mapper, NullLogger<UpdateSaleHandler>.Instance, _eventPublisher);

    private static Sale CreateSale(string saleNumber) =>
        new(saleNumber, DateTime.UtcNow,
            Guid.NewGuid(), "Customer",
            Guid.NewGuid(), "Branch",
            [new SaleItem(Guid.NewGuid(), "Product", 4, 100)]);

    private static UpdateSaleCommand BuildCommand(Guid id, string saleNumber) => new()
    {
        Id = id,
        SaleNumber = saleNumber,
        SaleDate = DateTime.UtcNow,
        CustomerId = Guid.NewGuid(),
        CustomerName = "Customer",
        BranchId = Guid.NewGuid(),
        BranchName = "Branch",
        Items = [new SaleItemInput { ProductId = Guid.NewGuid(), ProductName = "P", Quantity = 2, UnitPrice = 50 }]
    };
}
