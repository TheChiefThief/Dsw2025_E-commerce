using System.Linq.Expressions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ApplicationException = Dsw2025Tpi.Application.Exceptions.ApplicationException;

namespace Dsw2025Tpi.UnitTests.Services;

public class ProductsManagementServiceTests
{
    private readonly Mock<IRepository> _repoMock = new();
    private readonly Mock<ILogger<ProductsManagementService>> _loggerMock = new();
    private ProductsManagementService CreateService() =>
        new(_repoMock.Object, _loggerMock.Object);

    private static Product CreateProduct(
        Guid? id = null,
        string sku = "SKU-001",
        bool isActive = true,
        int stock = 10,
        decimal price = 100m) =>
        new(sku, "INT-001", "Laptop", "Notebook de prueba", price, stock)
        {
            Id = id ?? Guid.NewGuid(),
            IsActive = isActive
        };

    [Fact]
    public async Task GetProductById_ProductoActivo_DevuelveProducto()
    {
        // ARRANGE
        var productId = Guid.NewGuid();
        var product = CreateProduct(productId, price: 1200m, stock: 5);
        _repoMock
            .Setup(r => r.GetById<Product>(productId, It.IsAny<string[]>()))
            .ReturnsAsync(product);
        var service = CreateService();

        // ACT
        var resultado = await service.GetProductById(productId);

        // ASSERT
        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(productId);
        resultado.Name.Should().Be("Laptop");
        resultado.CurrentUnitPrice.Should().Be(1200m);
        _repoMock.Verify(r => r.GetById<Product>(productId, It.IsAny<string[]>()), Times.Once);
        Assert.True(false, "Fallo intencional para evidenciar Quality Gate");
    }

    [Fact]
    public async Task GetProductById_ProductoInexistente_LanzaEntityNotFoundException()
    {
        var productId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetById<Product>(productId, It.IsAny<string[]>()))
            .ReturnsAsync((Product?)null);
        var service = CreateService();

        var act = async () => await service.GetProductById(productId);

        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Producto no encontrado");
    }

    [Fact]
    public async Task GetProductById_ProductoInactivo_LanzaApplicationException()
    {
        var productId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetById<Product>(productId, It.IsAny<string[]>()))
            .ReturnsAsync(CreateProduct(productId, isActive: false));
        var service = CreateService();

        var act = async () => await service.GetProductById(productId);

        await act.Should().ThrowAsync<ApplicationException>()
            .WithMessage("El producto no está activo");
    }

    [Fact]
    public async Task GetAllProducts_ConProductosActivos_DevuelveLista()
    {
        var products = new List<Product>
        {
            CreateProduct(sku: "SKU-A"),
            CreateProduct(sku: "SKU-B")
        };
        _repoMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync(products);
        var service = CreateService();

        var resultado = await service.GetAllProducts();

        resultado.Should().NotBeNull();
        resultado!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllProducts_PageNumberInvalido_LanzaArgumentException()
    {
        var service = CreateService();
        var request = new ProductModel.SearchProduct(PageNumber: 0, PageSize: 10);

        var act = async () => await service.GetAllProducts(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("El número de página debe ser mayor que cero.");
    }

    [Fact]
    public async Task AddProduct_DatosValidos_DevuelveProductoCreado()
    {
        var request = new ProductModel.RequestProductModel(
            "SKU-NEW", "INT-NEW", "Mouse", "Mouse inalámbrico", 50m, 20, null);

        _repoMock
            .Setup(r => r.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((Product?)null);
        _repoMock
            .Setup(r => r.Add(It.IsAny<Product>()))
            .ReturnsAsync((Product p) => p);
        var service = CreateService();

        var resultado = await service.AddProduct(request);

        resultado.Sku.Should().Be("SKU-NEW");
        resultado.Name.Should().Be("Mouse");
        resultado.IsActive.Should().BeTrue();
        _repoMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Once);
    }

    [Fact]
    public async Task AddProduct_SkuDuplicado_LanzaDuplicatedEntityException()
    {
        var request = new ProductModel.RequestProductModel(
            "SKU-DUP", "INT-1", "Teclado", "Teclado mecánico", 80m, 5, null);

        _repoMock
            .Setup(r => r.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync(CreateProduct(sku: "SKU-DUP"));
        var service = CreateService();

        var act = async () => await service.AddProduct(request);

        await act.Should().ThrowAsync<DuplicatedEntityException>()
            .WithMessage("*SKU-DUP*");
        _repoMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task AddProduct_DatosInvalidos_LanzaBadRequestException()
    {
        var request = new ProductModel.RequestProductModel(
            "", "INT-1", "Nombre", "Desc", 10m, 1, null);
        var service = CreateService();

        var act = async () => await service.AddProduct(request);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("SKU is required.");
    }

    [Fact]
    public async Task UpdateProduct_ProductoExistente_ActualizaYDevuelve()
    {
        var productId = Guid.NewGuid();
        var existing = CreateProduct(productId, sku: "SKU-OLD");
        var request = new ProductModel.RequestProductModel(
            "SKU-UPD", "INT-UPD", "Laptop Pro", "Actualizado", 1500m, 8, "img.png");

        _repoMock
            .Setup(r => r.GetById<Product>(productId, It.IsAny<string[]>()))
            .ReturnsAsync(existing);
        _repoMock
            .Setup(r => r.First<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((Product?)null);
        _repoMock
            .Setup(r => r.Update(It.IsAny<Product>()))
            .ReturnsAsync((Product p) => p);
        var service = CreateService();

        var resultado = await service.UpdateProduct(productId, request);

        resultado.Sku.Should().Be("SKU-UPD");
        resultado.Name.Should().Be("Laptop Pro");
        resultado.CurrentUnitPrice.Should().Be(1500m);
        _repoMock.Verify(r => r.Update(It.IsAny<Product>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProduct_ProductoInexistente_LanzaEntityNotFoundException()
    {
        var productId = Guid.NewGuid();
        var request = new ProductModel.RequestProductModel(
            "SKU-1", "INT-1", "Name", "Desc", 10m, 1, null);
        _repoMock
            .Setup(r => r.GetById<Product>(productId, It.IsAny<string[]>()))
            .ReturnsAsync((Product?)null);
        var service = CreateService();

        var act = async () => await service.UpdateProduct(productId, request);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task PatchProduct_ProductoActivo_DeshabilitaProducto()
    {
        var productId = Guid.NewGuid();
        var product = CreateProduct(productId, isActive: true);
        _repoMock
            .Setup(r => r.GetById<Product>(productId, It.IsAny<string[]>()))
            .ReturnsAsync(product);
        _repoMock
            .Setup(r => r.Update(It.IsAny<Product>()))
            .ReturnsAsync((Product p) => p);
        var service = CreateService();

        await service.PatchProduct(productId);

        product.IsActive.Should().BeFalse();
        _repoMock.Verify(r => r.Update(It.Is<Product>(p => p.IsActive == false)), Times.Once);
    }

    [Fact]
    public async Task PatchProduct_ProductoYaDeshabilitado_LanzaApplicationException()
    {
        var productId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetById<Product>(productId, It.IsAny<string[]>()))
            .ReturnsAsync(CreateProduct(productId, isActive: false));
        var service = CreateService();

        var act = async () => await service.PatchProduct(productId);

        await act.Should().ThrowAsync<ApplicationException>()
            .WithMessage("El producto ya estaba deshabilitado");
    }
}
