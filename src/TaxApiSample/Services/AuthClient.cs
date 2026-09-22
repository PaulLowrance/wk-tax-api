using System.Net.Http.Json;
using System.Text.Json;
using TaxApiSample.Configuration;

namespace TaxApiSample.Services;

/// <summary>
/// Calls the Authentication API (Auth.json / AuthService) to obtain a session token
/// for firms using the CCH Axcess login method.
/// </summary>
public sealed class AuthClient
{
    private readonly HttpClient _httpClient;
    private readonly CchApiOptions _options;

    public AuthClient(HttpClient httpClient, CchApiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string> AuthenticateAsync(
        string integratorKey,
        string userName,
        string password,
        string? userSid,
        string? realm,
        CancellationToken cancellationToken)
    {
        var path = _options.UseAzureAuthenticate ? "/v1.0/AzureAuthenticate" : "/v1.0/Authenticate";
        var requestUri = _options.AuthBaseUrl.TrimEnd('/') + path;

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        if (!string.IsNullOrWhiteSpace(integratorKey))
        {
            request.Headers.Add("Integratorkey", integratorKey);
        }

        request.Content = JsonContent.Create(new
        {
            UserName = userName,
            UserSid = userSid ?? string.Empty,
            Password = password,
            Realm = realm ?? string.Empty,
            AdfsPilotLoginCode = string.Empty,
            IsInternal = false
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Authentication failed ({(int)response.StatusCode} {response.StatusCode}): {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.TryGetProperty("Token", out var tokenElement))
        {
            return tokenElement.ValueKind == JsonValueKind.String
                ? tokenElement.GetString() ?? string.Empty
                : tokenElement.GetRawText();
        }

        // Fall back to the raw body in case the response shape differs from the documented example.
        return responseBody;
    }
}
