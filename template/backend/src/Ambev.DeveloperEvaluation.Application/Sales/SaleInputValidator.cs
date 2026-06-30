using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales;

public static class SaleInputValidator
{
    public static IRuleBuilderOptions<T, IEnumerable<SaleItemInput>> HasValidSaleItems<T>(
        this IRuleBuilder<T, IEnumerable<SaleItemInput>> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .Must(items => items is not null && items.All(i => i.ProductId != Guid.Empty))
            .WithMessage("All sale items must have a product id")
            .Must(items => items is not null && items.All(i => !string.IsNullOrWhiteSpace(i.ProductName)))
            .WithMessage("All sale items must have a product name")
            .Must(items => items is not null && items.All(i => i.Quantity > 0))
            .WithMessage("All sale item quantities must be greater than zero")
            .Must(items => items is not null && items.All(i => i.Quantity <= 20))
            .WithMessage("It is not possible to sell more than 20 identical items")
            .Must(items => items is not null && items.All(i => i.UnitPrice > 0))
            .WithMessage("All sale item unit prices must be greater than zero");
    }
}
