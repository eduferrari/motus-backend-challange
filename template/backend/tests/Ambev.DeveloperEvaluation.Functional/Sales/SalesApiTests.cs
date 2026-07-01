using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.WebApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Sales;

public class SalesApiTests : IClassFixture<SalesApiFactory>
{
    private readonly HttpClient _client;

    public SalesApiTests(SalesApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "Given existing sales When requesting sales list Then returns paginated response")]
    public async Task GetSales_ShouldReturnPaginatedResponse()
    {
        var response = await _client.GetAsync("/api/Sales?_page=1&_size=10&_order=saleNumber asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.True(json.TryGetProperty("currentPage", out var currentPage), content);
        Assert.Equal(1, currentPage.GetInt32());
        Assert.Equal(1, json.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, json.GetProperty("totalCount").GetInt32());

        var sale = json.GetProperty("data")[0];
        Assert.Equal("S-FUNC-001", sale.GetProperty("saleNumber").GetString());
        Assert.Equal(360m, sale.GetProperty("totalAmount").GetDecimal());
    }
}

public sealed class SalesApiFactory : WebApplicationFactory<Program>
{
    private readonly InMemoryDatabaseRoot _databaseRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "functional-test-secret-key-with-enough-length",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=functional_tests"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DefaultContext>>();
            services.AddDbContext<DefaultContext>(options =>
                options.UseInMemoryDatabase("sales-functional", _databaseRoot));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            context.Database.EnsureCreated();
            context.Sales.Add(new Sale(
                "S-FUNC-001",
                DateTime.UtcNow,
                Guid.NewGuid(),
                "Functional Customer",
                Guid.NewGuid(),
                "Functional Branch",
                [new SaleItem(Guid.NewGuid(), "Functional Product", 4, 100)]));
            context.SaveChanges();
        });
    }
}
