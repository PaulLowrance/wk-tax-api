using System.Net.Http.Json;
using System.Text.Json;
using MyCCHTools.Configuration;
using MyCCHTools.Models;

namespace MyCCHTools.Services;

public sealed class AuthenticationClient(HttpClient httpClient)
{
    public async Task<AuthenticationResult> AuthenticateAsync(
        EnvironmentConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var request = new AuthenticationRequest(
            configuration.UserName,
            configuration.UserSid,
            configuration.Password,
            configuration.Realm,
            configuration.IsInternal);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(configuration.AuthenticationBaseUrl.TrimEnd('/') + "/"), "v1.0/Authenticate"));
        httpRequest.Headers.Add("Integratorkey", configuration.IntegratorKey);
        var requestJson = JsonSerializer.SerializeToUtf8Bytes(
            request,
            new JsonSerializerOptions { PropertyNamingPolicy = null });
        httpRequest.Content = new ByteArrayContent(requestJson);
        httpRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Authentication request failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). " +
                $"Response: {responseBody}",
                null,
                response.StatusCode);
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var token = ExtractToken(document.RootElement);
            return new AuthenticationResult(token, ReadExpiration(document.RootElement));
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Authentication response could not be parsed as JSON. Response: {responseBody}",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"Authentication response JSON was not in the expected format: {exception.Message} " +
                $"Response: {responseBody}",
                exception);
        }
    }

    private static string ExtractToken(JsonElement root)
    {
        if (!root.TryGetProperty("Token", out var tokenElement))
        {
            throw new InvalidOperationException("Authentication response did not contain a Token property.");
        }

        if (tokenElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tokenElement.GetString()))
        {
            return tokenElement.GetString()!;
        }

        if (tokenElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "Value", "AccessToken", "Token" })
            {
                if (tokenElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
                {
                    return property.GetString()!;
                }
            }
        }

        throw new InvalidOperationException("Authentication response contained an unusable Token property.");
    }

    private static DateTimeOffset? ReadExpiration(JsonElement root)
    {
        foreach (var propertyName in new[] { "ExpiresAt", "Expiration", "Expires" })
        {
            if (root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(property.GetString(), out var expiration))
            {
                return expiration;
            }
        }

        return null;
    }
}

public sealed record AuthenticationResult(string Token, DateTimeOffset? ExpiresAt);