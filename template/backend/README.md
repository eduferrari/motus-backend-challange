# DeveloperStore Sales API

Backend implementation for the DeveloperStore sales records challenge. The solution follows the template's layered DDD-style structure and implements complete CRUD plus cancellation operations for sales.

## What Is Implemented

- Create, retrieve, update, and delete sales records.
- Cancel an entire sale.
- Cancel a single sale item.
- Sale records include sale number, sale date, customer external identity and denormalized name, branch external identity and denormalized name, total amount, cancellation status, and item details.
- Sale items include product external identity and denormalized name, quantity, unit price, discount, total amount, and cancellation status.
- Domain-enforced quantity rules:
  - 1 to 3 identical items: no discount.
  - 4 to 9 identical items: 10% discount.
  - 10 to 20 identical items: 20% discount.
  - More than 20 identical items is rejected.
- Sales domain events are represented as structured application logs:
  - `SaleCreated`
  - `SaleModified`
  - `SaleCancelled`
  - `ItemCancelled`

## Tech Stack

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- MediatR
- AutoMapper
- FluentValidation
- Serilog
- xUnit, FluentAssertions, NSubstitute
- Docker Compose

## Project Structure

```text
src/
  Ambev.DeveloperEvaluation.Domain/       Domain entities, business rules, repositories contracts
  Ambev.DeveloperEvaluation.Application/  Use cases, commands, handlers, validators, DTOs
  Ambev.DeveloperEvaluation.ORM/          EF Core context, mappings, repositories, migrations
  Ambev.DeveloperEvaluation.WebApi/       Controllers, requests, responses, API mappings
  Ambev.DeveloperEvaluation.IoC/          Dependency registration
  Ambev.DeveloperEvaluation.Common/       Shared validation, logging, auth, health checks
tests/
  Ambev.DeveloperEvaluation.Unit/         Unit tests for domain and application behavior
  Ambev.DeveloperEvaluation.Integration/  Integration test project placeholder
  Ambev.DeveloperEvaluation.Functional/   Functional test project placeholder
```

## Prerequisites

- .NET SDK 8
- Docker and Docker Compose

## Configuration

The default connection string is defined in `src/Ambev.DeveloperEvaluation.WebApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=developer_evaluation;User Id=developer;Password=ev@luAt10n"
  }
}
```

Docker Compose starts PostgreSQL with matching credentials and maps it to `localhost:5432`.

## Running Locally

Start the infrastructure:

```bash
docker compose up -d ambev.developerevaluation.database ambev.developerevaluation.nosql ambev.developerevaluation.cache
```

Restore and build:

```bash
dotnet restore Ambev.DeveloperEvaluation.sln
dotnet build Ambev.DeveloperEvaluation.sln
```

Apply the EF Core migrations:

```bash
dotnet ef database update \
  --project src/Ambev.DeveloperEvaluation.ORM \
  --startup-project src/Ambev.DeveloperEvaluation.WebApi
```

Run the API:

```bash
dotnet run --project src/Ambev.DeveloperEvaluation.WebApi
```

Swagger is available in Development at:

```text
http://localhost:5119/swagger
```

The exact local HTTP/HTTPS URLs may also be shown by `dotnet run` based on `launchSettings.json`.

## Running With Docker Compose

```bash
docker compose up --build
```

The Web API is mapped to:

```text
http://localhost:8080
```

## Testing

Run all tests:

```bash
dotnet test Ambev.DeveloperEvaluation.sln
```

Run only unit tests:

```bash
dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj
```

Generate coverage:

```bash
./coverage-report.sh
```

## Sales API Examples

Create a sale:

```http
POST /api/Sales
Content-Type: application/json

{
  "saleNumber": "S-0001",
  "saleDate": "2026-06-30T12:00:00Z",
  "customerId": "11111111-1111-1111-1111-111111111111",
  "customerName": "ACME Customer",
  "branchId": "22222222-2222-2222-2222-222222222222",
  "branchName": "Main Branch",
  "items": [
    {
      "productId": "33333333-3333-3333-3333-333333333333",
      "productName": "Product A",
      "quantity": 10,
      "unitPrice": 100
    },
    {
      "productId": "44444444-4444-4444-4444-444444444444",
      "productName": "Product B",
      "quantity": 3,
      "unitPrice": 50
    }
  ]
}
```

Retrieve, update, delete, and cancel:

```http
GET /api/Sales/{id}
PUT /api/Sales/{id}
DELETE /api/Sales/{id}
PATCH /api/Sales/{id}/cancel
PATCH /api/Sales/{saleId}/items/{itemId}/cancel
```

## Notes

- Customer, branch, and product references use external IDs plus denormalized descriptions, matching the External Identities pattern described in the challenge.
- The current optional event behavior logs event names through the application logger; no message broker is required.
- The solution contains an existing dependency warning for `AutoMapper` 13.0.1 reported by `dotnet restore/test`.
