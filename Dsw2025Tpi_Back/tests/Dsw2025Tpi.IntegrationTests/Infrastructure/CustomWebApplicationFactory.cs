using Dsw2025Tpi.Api;
using Dsw2025Tpi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2025Tpi.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"Dsw2025Tpi_IntegrationTests_{Guid.NewGuid()}";
    private readonly string _authDbName = $"Dsw2025Tpi_Auth_IntegrationTests_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistration<Dsw2025TpiContext>(services);
            RemoveDbContextRegistration<AuthenticateContext>(services);

            services.AddDbContext<Dsw2025TpiContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.AddDbContext<AuthenticateContext>(options =>
                options.UseInMemoryDatabase(_authDbName));
        });
    }

    private static void RemoveDbContextRegistration<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        var descriptors = services
            .Where(d =>
                d.ServiceType == typeof(TContext) ||
                d.ServiceType == typeof(DbContextOptions<TContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(IDbContextOptionsConfiguration<TContext>))
            .ToList();

        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }
}
