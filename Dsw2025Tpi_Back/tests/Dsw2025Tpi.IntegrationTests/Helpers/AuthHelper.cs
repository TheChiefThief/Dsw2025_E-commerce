using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Dsw2025Tpi.IntegrationTests.Helpers;

public record LoginResponse(string Token, string Role, string Name, Guid CustomerId);

public static class AuthHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<LoginResponse> LoginAsAdminAsync(HttpClient client)
    {
        return await LoginAsync(client, "admin", "Admin123!");
    }

    public static async Task<LoginResponse> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions)
            ?? throw new InvalidOperationException("No se pudo deserializar la respuesta de login.");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.Token);

        return payload;
    }

    public static async Task RegisterAsync(HttpClient client, string username, string password, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            password,
            email
        });

        response.EnsureSuccessStatusCode();
    }
}
