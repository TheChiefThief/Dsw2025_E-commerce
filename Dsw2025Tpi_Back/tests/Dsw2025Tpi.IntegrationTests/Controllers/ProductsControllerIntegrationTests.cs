using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.IntegrationTests.Helpers;
using Dsw2025Tpi.IntegrationTests.Infrastructure;

namespace Dsw2025Tpi.IntegrationTests.Controllers;

public class ProductsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ProductsControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProductos_DevuelveCodigo200OK()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task GetProductoById_SinToken_Devuelve401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddProduct_ConAdmin_Devuelve201Created()
    {
        var client = _factory.CreateClient();
        await AuthHelper.LoginAsAdminAsync(client);

        var request = new ProductModel.RequestProductModel(
            Sku: $"SKU-{Guid.NewGuid():N}"[..12],
            InternalCode: "INT-TEST",
            Name: "Monitor Test",
            Description: "Monitor de integración",
            CurrentUnitPrice: 250m,
            StockQuantity: 15);

        var response = await client.PostAsJsonAsync("/api/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductModel.ResponseProductModel>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("Monitor Test", product!.Name);
        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task AddProduct_SinToken_Devuelve401Unauthorized()
    {
        var client = _factory.CreateClient();

        var request = new ProductModel.RequestProductModel(
            "SKU-NOAUTH", "INT-1", "Producto", "Desc", 10m, 1);

        var response = await client.PostAsJsonAsync("/api/products", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_DevuelveCodigo200OK()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthcheck");

        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
