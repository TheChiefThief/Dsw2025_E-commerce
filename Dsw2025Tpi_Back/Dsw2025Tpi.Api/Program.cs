using Dsw2025Tpi.Api.DependencyInjection;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Data.Helpers;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;


namespace Dsw2025Tpi.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((context, configuration) =>
            configuration.ReadFrom.Configuration(context.Configuration)
                .WriteTo.Console()
                .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day));

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Dsw2025TPI - FLJ",
                Version = "v4",
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Ingresar el token",
                Type = SecuritySchemeType.ApiKey
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
        });
        builder.Services.AddHealthChecks();
        builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password = new PasswordOptions
            {
                RequiredLength = 8
            };

        })
            .AddEntityFrameworkStores<AuthenticateContext>()
            .AddDefaultTokenProviders();
        
        builder.Services.AddDomainServices(builder.Configuration);
        builder.Services.AddDbContext<AuthenticateContext>(options => {
            options.UseSqlServer(builder.Configuration.GetConnectionString("Dsw2025Tpi"));
        });
        builder.Services.AddSingleton<JwtTokenService>();
        var jwtConfig = builder.Configuration.GetSection("Jwt");
        var keyText = jwtConfig["Key"] ?? throw new ArgumentNullException("JWT Key");
        var key = Encoding.UTF8.GetBytes(keyText);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtConfig["Issuer"],
                ValidAudience = jwtConfig["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key),
                RoleClaimType = ClaimTypes.Role
            };
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("PermitirFrontend", policy =>
            {
                policy.WithOrigins("http://localhost:5173")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        builder.Services.AddTransient<CustomExceptionHandlingMiddleware>();

        var app = builder.Build();

        Log.Information("Starting up");

        try
        {
            var rolesToCreate = builder.Configuration.GetSection("Roles").Get<List<string>>();

            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
                dbContext.Database.Migrate();
                var authContext = scope.ServiceProvider.GetRequiredService<AuthenticateContext>();
                authContext.Database.Migrate();
                dbContext.Seedwork<Product>("Sources/products.json");
                dbContext.Seedwork<Customer>("Sources/customers.json");
                dbContext.Seedwork<Order>("Sources/orders.json");

                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                foreach (var roleName in rolesToCreate!)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                    }
                }

                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var adminSection = builder.Configuration.GetSection("DefaultAdminUser");

                var adminUser = await userManager.FindByNameAsync(adminSection["UserName"]);
                if (adminUser == null)
                {
                    var newUser = new IdentityUser
                    {
                        UserName = adminSection["UserName"],
                        Email = adminSection["Email"],
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(newUser, adminSection["Password"]);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(newUser, adminSection["Role"]);
                    }
                }
            }

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseCors("PermitirFrontend");
            
            app.UseSerilogRequestLogging();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<CustomExceptionHandlingMiddleware>();

            app.MapControllers();
            app.MapHealthChecks("/healthcheck");

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Ejecución fallida de la aplicación");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}