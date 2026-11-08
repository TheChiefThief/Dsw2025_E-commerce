using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.IntegrationTests.Helpers;
using Dsw2025Tpi.IntegrationTests.Infrastructure;

namespace Dsw2025Tpi.IntegrationTests.Controllers;

public class OrdersControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public OrdersControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AddOrder_ConProductoYClienteValidos_Devuelve201Created()
    {
        var client = _factory.CreateClient();
        var login = await AuthHelper.LoginAsAdminAsync(client);

        var productRequest = new ProductModel.RequestProductModel(
            Sku: $"SKU-{Guid.NewGuid():N}"[..12],
            InternalCode: "INT-ORD",
            Name: "Teclado Order",
            Description: "Producto para orden",
            CurrentUnitPrice: 80m,
            StockQuantity: 20);

        var productResponse = await client.PostAsJsonAsync("/api/products", productRequest);
        productResponse.EnsureSuccessStatusCode();
        var product = await productResponse.Content
            .ReadFromJsonAsync<ProductModel.ResponseProductModel>(JsonOptions);

        var orderRequest = new OrderModel.RequestOrderModel(
            ShippingAddress: "Calle Falsa 123",
            BillingAddress: "Calle Falsa 123",
            Notes: "Entrega express",
            CustomerId: login.CustomerId,
            OrderItems:
            [
                new OrderItemModel.RequestOrderItemModel(2, product!.Id)
            ]);

        var response = await client.PostAsJsonAsync("/api/orders", orderRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderModel.ResponseOrderModel>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal(login.CustomerId, order!.CustomerId);
        Assert.Single(order.OrderItems);
        Assert.Equal(160m, order.TotatAmount);
    }

    [Fact]
    public async Task GetOrderById_SinToken_Devuelve401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
