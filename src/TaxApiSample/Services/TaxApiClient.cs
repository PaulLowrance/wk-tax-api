using System.Text;
using System.Text.Json;
using TaxApiSample.Configuration;

namespace TaxApiSample.Services;

/// <summary>
/// Invokes an operation described by <see cref="TaxApiEndpointCatalog"/> against the
/// Tax Services v2 API, attaching the IntegratorKey and session/OAuth headers.
/// </summary>
public sealed class TaxApiClient
{
    private readonly HttpClient _httpClient;
    private readonly CchApiOptions _options;

    public TaxApiClient(HttpClient httpClient, CchApiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<(int StatusCode, string Body)> InvokeAsync(
        EndpointInfo endpoint,
        EndpointOperation operation,
        IReadOnlyList<(string Key, string Value)> queryParams,
        string? bodyJson,
        string integratorKey,
        string? securityToken,
        string? authorizationToken,
        CancellationToken cancellationToken)
    {
        var uriBuilder = new StringBuilder(_options.TaxServiceBaseUrl.TrimEnd('/')).Append(endpoint.Path);

        if (queryParams.Count > 0)
        {
            uriBuilder.Append('?');
            uriBuilder.Append(string.Join('&', queryParams.Select(p =>
                $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}")));
        }

        using var request = new HttpRequestMessage(new HttpMethod(operation.Method), uriBuilder.ToString());

        if (!string.IsNullOrWhiteSpace(integratorKey))
        {
            request.Headers.Add("IntegratorKey", integratorKey);
        }

        if (!string.IsNullOrWhiteSpace(authorizationToken))
        {
            request.Headers.Add("Authorization", authorizationToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authorizationToken
                : $"Bearer {authorizationToken}");
        }
        else if (!string.IsNullOrWhiteSpace(securityToken))
        {
            request.Headers.Add("Security", securityToken);
        }

        if (!string.IsNullOrWhiteSpace(bodyJson))
        {
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return ((int)response.StatusCode, Pretty(responseBody));
    }

    private static string Pretty(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
