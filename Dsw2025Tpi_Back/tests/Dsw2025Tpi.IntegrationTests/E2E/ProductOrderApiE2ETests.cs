using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Helpers;
using Dsw2025Tpi.IntegrationTests.Infrastructure;

namespace Dsw2025Tpi.IntegrationTests.E2E;

/// <summary>
/// E2E a nivel de API: encadena login → crear producto → crear orden → consultar orden.
/// Sin frontend; solo requests HTTP sintéticas contra la API en memoria.
/// </summary>
public class ProductOrderApiE2ETests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProductOrderApiE2ETests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FlujoCritico_Login_CrearProducto_CrearOrden_ConsultarOrden_EsCoherente()
    {
        var client = _factory.CreateClient();

        // 1) LOGIN
        var login = await AuthHelper.LoginAsAdminAsync(client);
        Assert.False(string.IsNullOrWhiteSpace(login.Token));
        Assert.Equal("Admin", login.Role);
        Assert.NotEqual(Guid.Empty, login.CustomerId);

        // 2) CREAR PRODUCTO
        var productRequest = new ProductModel.RequestProductModel(
            Sku: $"SKU-{Guid.NewGuid():N}"[..12],
            InternalCode: "INT-E2E",
            Name: "Notebook E2E",
            Description: "Producto del flujo E2E API",
            CurrentUnitPrice: 1200m,
            StockQuantity: 5);

        var createProductResponse = await client.PostAsJsonAsync("/api/products", productRequest);
        Assert.Equal(HttpStatusCode.Created, createProductResponse.StatusCode);

        var createdProduct = await createProductResponse.Content
            .ReadFromJsonAsync<ProductModel.ResponseProductModel>(JsonOptions);
        Assert.NotNull(createdProduct);

        // 3) CONSULTAR PRODUCTO POR ID
        var getProductResponse = await client.GetAsync($"/api/products/{createdProduct!.Id}");
        Assert.Equal(HttpStatusCode.OK, getProductResponse.StatusCode);

        var fetchedProduct = await getProductResponse.Content
            .ReadFromJsonAsync<ProductModel.ResponseProductModel>(JsonOptions);
        Assert.Equal(createdProduct.Id, fetchedProduct!.Id);
        Assert.Equal("Notebook E2E", fetchedProduct.Name);

        // 4) CREAR ORDEN
        var orderRequest = new OrderModel.RequestOrderModel(
            ShippingAddress: "San Martín 100",
            BillingAddress: "San Martín 100",
            Notes: "Flujo E2E",
            CustomerId: login.CustomerId,
            OrderItems:
            [
                new OrderItemModel.RequestOrderItemModel(1, createdProduct.Id)
            ]);

        var createOrderResponse = await client.PostAsJsonAsync("/api/orders", orderRequest);
        Assert.Equal(HttpStatusCode.Created, createOrderResponse.StatusCode);

        var createdOrder = await createOrderResponse.Content
            .ReadFromJsonAsync<OrderModel.ResponseOrderModel>(JsonOptions);
        Assert.NotNull(createdOrder);
        Assert.Equal(OrderStatus.PENDING, createdOrder!.Status);
        Assert.Equal(1200m, createdOrder.TotatAmount);

        // 5) CONSULTAR ORDEN POR ID
        var getOrderResponse = await client.GetAsync($"/api/orders/{createdOrder.Id}");
        Assert.Equal(HttpStatusCode.OK, getOrderResponse.StatusCode);

        var fetchedOrder = await getOrderResponse.Content
            .ReadFromJsonAsync<OrderModel.ResponseOrderModel>(JsonOptions);
        Assert.Equal(createdOrder.Id, fetchedOrder!.Id);
        Assert.Equal(login.CustomerId, fetchedOrder.CustomerId);
        Assert.Single(fetchedOrder.OrderItems);
        Assert.Equal(createdProduct.Id, fetchedOrder.OrderItems[0].ProductId);
    }

    [Fact]
    public async Task FlujoRegistroYLogin_UsuarioNuevo_PuedeAutenticarse()
    {
        var client = _factory.CreateClient();
        var username = $"user_{Guid.NewGuid():N}"[..12];
        var email = $"{username}@test.com";
        const string password = "User1234!";

        // 1) REGISTER
        await AuthHelper.RegisterAsync(client, username, password, email);

        // 2) LOGIN
        var login = await AuthHelper.LoginAsync(client, username, password);

        Assert.Equal("User", login.Role);
        Assert.False(string.IsNullOrWhiteSpace(login.Token));
        Assert.NotEqual(Guid.Empty, login.CustomerId);
    }
}
