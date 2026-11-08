using System.Linq.Expressions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Dsw2025Tpi.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using ApplicationException = Dsw2025Tpi.Application.Exceptions.ApplicationException;

namespace Dsw2025Tpi.UnitTests.Services;

public class OrdersManagementServiceTests
{
    private readonly Mock<IRepository> _repoMock = new();
    private readonly Mock<UserManager<IdentityUser>> _userManagerMock = MockUserManagerFactory.Create();
    private readonly Mock<ILogger<OrdersManagementService>> _loggerMock = new();

    private OrdersManagementService CreateService() =>
        new(_repoMock.Object, _userManagerMock.Object, _loggerMock.Object);

    private static Customer CreateCustomer(Guid? id = null, string email = "cliente@test.com") =>
        new(email, "Cliente Test", "123456")
        {
            Id = id ?? Guid.NewGuid()
        };

    private static Product CreateProduct(
        Guid? id = null,
        bool isActive = true,
        int stock = 10,
        decimal price = 100m,
        string name = "Producto") =>
        new("SKU-1", "INT-1", name, "Desc", price, stock)
        {
            Id = id ?? Guid.NewGuid(),
            IsActive = isActive
        };

    private static Order CreateOrder(Customer customer, Product product, int quantity = 1)
    {
        var order = new Order("Calle 1", "Calle 1", "Nota", customer.Id)
        {
            Customer = customer
        };
        order.OrderItems =
        [
            new OrderItem(quantity, product.CurrentUnitPrice, order.Id, product.Id)
            {
                Product = product
            }
        ];
        return order;
    }

    [Fact]
    public async Task GetOrderById_OrdenExistente_DevuelveOrden()
    {
        // ARRANGE
        var customer = CreateCustomer();
        var product = CreateProduct();
        var order = CreateOrder(customer, product);

        _repoMock
            .Setup(r => r.GetById<Order>(
                order.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(order);
        _userManagerMock
            .Setup(u => u.FindByEmailAsync(customer.Email!))
            .ReturnsAsync(new IdentityUser { UserName = "user1", Email = customer.Email });
        var service = CreateService();

        // ACT
        var resultado = await service.GetOrderById(order.Id);

        // ASSERT
        resultado.Should().NotBeNull();
        resultado.Id.Should().Be(order.Id);
        resultado.CustomerName.Should().Be("Cliente Test");
        resultado.UserName.Should().Be("user1");
        resultado.OrderItems.Should().HaveCount(1);
        _repoMock.Verify(r => r.GetById<Order>(order.Id, It.IsAny<string[]>()), Times.Once);
    }

    [Fact]
    public async Task GetOrderById_OrdenInexistente_LanzaEntityNotFoundException()
    {
        var orderId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetById<Order>(orderId, It.IsAny<string[]>()))
            .ReturnsAsync((Order?)null);
        var service = CreateService();

        var act = async () => await service.GetOrderById(orderId);

        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Orden no encontrada");
    }

    [Fact]
    public async Task AddOrder_DatosValidos_CreaOrdenYDescuentaStock()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(stock: 10, price: 200m);
        var request = new OrderModel.RequestOrderModel(
            "Av. Siempre Viva 742",
            "Av. Siempre Viva 742",
            "Entregar mañana",
            customer.Id,
            [new OrderItemModel.RequestOrderItemModel(2, product.Id)]);

        _repoMock
            .Setup(r => r.GetById<Customer>(customer.Id, It.IsAny<string[]>()))
            .ReturnsAsync(customer);
        _repoMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync(new List<Product> { product });
        _repoMock
            .Setup(r => r.Update(It.IsAny<Product>()))
            .ReturnsAsync((Product p) => p);
        _repoMock
            .Setup(r => r.Add(It.IsAny<Order>()))
            .ReturnsAsync((Order o) => o);
        _userManagerMock
            .Setup(u => u.FindByEmailAsync(customer.Email!))
            .ReturnsAsync(new IdentityUser { UserName = "user1", Email = customer.Email });
        var service = CreateService();

        var resultado = await service.AddOrder(request);

        resultado.CustomerId.Should().Be(customer.Id);
        resultado.OrderItems.Should().HaveCount(1);
        resultado.TotatAmount.Should().Be(400m);
        product.StockQuantity.Should().Be(8);
        _repoMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Once);
        _repoMock.Verify(r => r.Update(It.IsAny<Product>()), Times.Once);
    }

    [Fact]
    public async Task AddOrder_ClienteInexistente_LanzaEntityNotFoundException()
    {
        var customerId = Guid.NewGuid();
        var request = new OrderModel.RequestOrderModel(
            "Dir 1", "Dir 1", null, customerId,
            [new OrderItemModel.RequestOrderItemModel(1, Guid.NewGuid())]);

        _repoMock
            .Setup(r => r.GetById<Customer>(customerId, It.IsAny<string[]>()))
            .ReturnsAsync((Customer?)null);
        var service = CreateService();

        var act = async () => await service.AddOrder(request);

        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage($"*Cliente con ID {customerId}*");
    }

    [Fact]
    public async Task AddOrder_StockInsuficiente_LanzaInvalidOperationException()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(stock: 1, name: "Laptop");
        var request = new OrderModel.RequestOrderModel(
            "Dir 1", "Dir 1", null, customer.Id,
            [new OrderItemModel.RequestOrderItemModel(5, product.Id)]);

        _repoMock
            .Setup(r => r.GetById<Customer>(customer.Id, It.IsAny<string[]>()))
            .ReturnsAsync(customer);
        _repoMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync(new List<Product> { product });
        var service = CreateService();

        var act = async () => await service.AddOrder(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Stock insuficiente*Laptop*");
        _repoMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task AddOrder_ProductoDeshabilitado_LanzaApplicationException()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(isActive: false, name: "Mouse");
        var request = new OrderModel.RequestOrderModel(
            "Dir 1", "Dir 1", null, customer.Id,
            [new OrderItemModel.RequestOrderItemModel(1, product.Id)]);

        _repoMock
            .Setup(r => r.GetById<Customer>(customer.Id, It.IsAny<string[]>()))
            .ReturnsAsync(customer);
        _repoMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync(new List<Product> { product });
        var service = CreateService();

        var act = async () => await service.AddOrder(request);

        await act.Should().ThrowAsync<ApplicationException>()
            .WithMessage("*Mouse*deshabilitado*");
    }

    [Fact]
    public async Task AddOrder_SinItems_LanzaInvalidOperationException()
    {
        var request = new OrderModel.RequestOrderModel(
            "Dir 1", "Dir 1", null, Guid.NewGuid(), []);
        var service = CreateService();

        var act = async () => await service.AddOrder(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least one item*");
    }

    [Fact]
    public async Task UpdateOrderStatus_CancelarOrden_DevuelveStock()
    {
        var customer = CreateCustomer();
        var product = CreateProduct(stock: 5);
        var order = CreateOrder(customer, product, quantity: 2);
        var stockAntes = product.StockQuantity;

        _repoMock
            .Setup(r => r.GetById<Order>(order.Id, It.IsAny<string[]>()))
            .ReturnsAsync(order);
        _repoMock
            .Setup(r => r.GetById<Product>(product.Id, It.IsAny<string[]>()))
            .ReturnsAsync(product);
        _repoMock
            .Setup(r => r.Update(It.IsAny<Product>()))
            .ReturnsAsync((Product p) => p);
        _repoMock
            .Setup(r => r.Update(It.IsAny<Order>()))
            .ReturnsAsync((Order o) => o);
        _userManagerMock
            .Setup(u => u.FindByEmailAsync(customer.Email!))
            .ReturnsAsync(new IdentityUser { UserName = "user1", Email = customer.Email });
        var service = CreateService();

        var resultado = await service.UpdateOrderStatus(order.Id, "CANCELLED");

        resultado.Status.Should().Be(OrderStatus.CANCELLED);
        product.StockQuantity.Should().Be(stockAntes + 2);
        _repoMock.Verify(r => r.Update(It.IsAny<Product>()), Times.Once);
        _repoMock.Verify(r => r.Update(It.IsAny<Order>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatus_EstadoInvalido_LanzaArgumentException()
    {
        var customer = CreateCustomer();
        var product = CreateProduct();
        var order = CreateOrder(customer, product);

        _repoMock
            .Setup(r => r.GetById<Order>(order.Id, It.IsAny<string[]>()))
            .ReturnsAsync(order);
        var service = CreateService();

        var act = async () => await service.UpdateOrderStatus(order.Id, "NO_EXISTE");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("El estado ingresado no es válido");
    }
}
