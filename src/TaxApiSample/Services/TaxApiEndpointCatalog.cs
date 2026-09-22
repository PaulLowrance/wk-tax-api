using System.Reflection;
using System.Text.Json;

namespace TaxApiSample.Services;

/// <summary>
/// Describes a single query or header parameter declared for an operation in the swagger document.
/// </summary>
public sealed record EndpointParameter(string Name, string In, bool Required);

/// <summary>
/// Describes one HTTP method available on a Tax Services v2 endpoint.
/// </summary>
public sealed record EndpointOperation(string Method, string Summary, IReadOnlyList<EndpointParameter> Parameters, bool HasBody);

/// <summary>
/// Describes an endpoint (path) and every HTTP method it supports.
/// </summary>
public sealed record EndpointInfo(string Name, string Path, IReadOnlyDictionary<string, EndpointOperation> Operations);

/// <summary>
/// Loads the embedded tsv2.json OpenAPI document and exposes it as a simple
/// name -> path/method/parameter catalog so the CLI can dispatch calls without
/// hand-written command classes per endpoint.
/// </summary>
public sealed class TaxApiEndpointCatalog
{
    private readonly Dictionary<string, EndpointInfo> _endpointsByName;

    public TaxApiEndpointCatalog()
    {
        using var stream = typeof(TaxApiEndpointCatalog).Assembly
            .GetManifestResourceStream("TaxApiSample.Swagger.tsv2.json")
            ?? throw new InvalidOperationException("Embedded tsv2.json swagger document was not found.");

        using var document = JsonDocument.Parse(stream);
        _endpointsByName = Parse(document.RootElement);
    }

    public IReadOnlyCollection<EndpointInfo> Endpoints => _endpointsByName.Values;

    public bool TryGet(string name, out EndpointInfo endpoint) =>
        _endpointsByName.TryGetValue(name, out endpoint!);

    private static Dictionary<string, EndpointInfo> Parse(JsonElement root)
    {
        var result = new Dictionary<string, EndpointInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var pathProperty in root.GetProperty("paths").EnumerateObject())
        {
            var path = pathProperty.Name;
            var name = path.TrimEnd('/').Split('/').Last();

            var operations = new Dictionary<string, EndpointOperation>(StringComparer.OrdinalIgnoreCase);
            foreach (var methodProperty in pathProperty.Value.EnumerateObject())
            {
                var method = methodProperty.Name.ToUpperInvariant();
                if (method is not ("GET" or "POST" or "PUT" or "DELETE" or "PATCH"))
                {
                    continue;
                }

                var operation = methodProperty.Value;
                var summary = operation.TryGetProperty("summary", out var summaryEl) ? summaryEl.GetString() ?? string.Empty : string.Empty;

                var parameters = new List<EndpointParameter>();
                if (operation.TryGetProperty("parameters", out var parametersEl))
                {
                    foreach (var parameter in parametersEl.EnumerateArray())
                    {
                        var paramName = parameter.GetProperty("name").GetString() ?? string.Empty;
                        var paramIn = parameter.GetProperty("in").GetString() ?? string.Empty;
                        var required = parameter.TryGetProperty("required", out var reqEl) && reqEl.GetBoolean();

                        // The Security/Authorization/IntegratorKey headers are handled automatically by the client.
                        if (paramIn == "header")
                        {
                            continue;
                        }

                        parameters.Add(new EndpointParameter(paramName, paramIn, required));
                    }
                }

                var hasBody = operation.TryGetProperty("requestBody", out _);

                operations[method] = new EndpointOperation(method, summary, parameters, hasBody);
            }

            result[name] = new EndpointInfo(name, path, operations);
        }

        return result;
    }
}
