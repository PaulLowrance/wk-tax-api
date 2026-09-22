using DotNetEnv;
using Microsoft.Extensions.Configuration;
using TaxApiSample.Configuration;
using TaxApiSample.Services;

namespace TaxApiSample;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Load .env (if present) into process environment variables. Secrets such as
        // INTEGRATOR_KEY and login credentials live here and are never committed.
        var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }
        else if (File.Exists(".env"))
        {
            Env.Load(".env");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var options = configuration.GetSection(CchApiOptions.SectionName).Get<CchApiOptions>()
            ?? new CchApiOptions();

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();

        try
        {
            return command.ToLowerInvariant() switch
            {
                "help" or "-h" or "--help" => PrintUsage(),
                "list" or "endpoints" => ListEndpoints(),
                "login" => await LoginAsync(options),
                "logout" => Logout(),
                _ => await InvokeEndpointAsync(options, command, rest),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int PrintUsage()
    {
        Console.WriteLine("""
            TaxApiSample - CLI client for the CCH Axcess Tax Services v2 API

            Usage:
              TaxApiSample login
              TaxApiSample logout
              TaxApiSample list
              TaxApiSample <endpoint-name> [--method GET|POST|PUT|DELETE]
                                            [--query key=value ...]
                                            [--body '<json>' | --body @file.json]
                                            [--security <token>] [--authorization <token>]
                                            [--integrator-key <key>]

            Examples:
              TaxApiSample login
              TaxApiSample Returns --query "$filter=TaxYear eq '2023'"
              TaxApiSample CalculateReturn --body @calculate-request.json

            Run "TaxApiSample list" to see every available endpoint and its HTTP methods.
            """);
        return 0;
    }

    private static int ListEndpoints()
    {
        var catalog = new TaxApiEndpointCatalog();
        foreach (var endpoint in catalog.Endpoints.OrderBy(e => e.Name))
        {
            Console.WriteLine($"{endpoint.Name}  ({endpoint.Path})");
            foreach (var op in endpoint.Operations.Values)
            {
                var paramList = op.Parameters.Count == 0
                    ? "(no query params)"
                    : string.Join(", ", op.Parameters.Select(p => p.Required ? $"{p.Name}*" : p.Name));
                Console.WriteLine($"  {op.Method,-6} {op.Summary}");
                Console.WriteLine($"         query: {paramList}{(op.HasBody ? ", has request body" : string.Empty)}");
            }
        }

        return 0;
    }

    private static async Task<int> LoginAsync(CchApiOptions options)
    {
        var integratorKey = Environment.GetEnvironmentVariable("INTEGRATOR_KEY") ?? string.Empty;
        var userName = Environment.GetEnvironmentVariable("CCH_USERNAME");
        var password = Environment.GetEnvironmentVariable("CCH_PASSWORD");
        var userSid = Environment.GetEnvironmentVariable("CCH_USER_SID");
        var realm = Environment.GetEnvironmentVariable("CCH_REALM");

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("CCH_USERNAME and CCH_PASSWORD must be set (see .env.example).");
            return 1;
        }

        using var httpClient = new HttpClient();
        var authClient = new AuthClient(httpClient, options);
        var token = await authClient.AuthenticateAsync(integratorKey, userName, password, userSid, realm, CancellationToken.None);

        new TokenCache().Save(token);
        Console.WriteLine("Login succeeded. Session token cached for subsequent commands.");
        return 0;
    }

    private static int Logout()
    {
        new TokenCache().Clear();
        Console.WriteLine("Cached session token removed.");
        return 0;
    }

    private static async Task<int> InvokeEndpointAsync(CchApiOptions options, string endpointName, string[] args)
    {
        var catalog = new TaxApiEndpointCatalog();
        if (!catalog.TryGet(endpointName, out var endpoint))
        {
            Console.Error.WriteLine($"Unknown endpoint '{endpointName}'. Run 'TaxApiSample list' to see available endpoints.");
            return 1;
        }

        var parsed = ParseArgs(args);

        var method = parsed.Method;
        if (method is null)
        {
            if (endpoint.Operations.Count == 1)
            {
                method = endpoint.Operations.Keys.Single();
            }
            else
            {
                Console.Error.WriteLine(
                    $"Endpoint '{endpointName}' supports multiple methods ({string.Join(", ", endpoint.Operations.Keys)}). " +
                    "Specify one with --method.");
                return 1;
            }
        }

        if (!endpoint.Operations.TryGetValue(method, out var operation))
        {
            Console.Error.WriteLine($"Endpoint '{endpointName}' does not support method '{method}'.");
            return 1;
        }

        var integratorKey = parsed.IntegratorKey ?? Environment.GetEnvironmentVariable("INTEGRATOR_KEY") ?? string.Empty;
        var securityToken = parsed.Security ?? new TokenCache().Load();

        if (string.IsNullOrWhiteSpace(integratorKey))
        {
            Console.Error.WriteLine("No integrator key found. Set INTEGRATOR_KEY in .env or pass --integrator-key.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(securityToken) && string.IsNullOrWhiteSpace(parsed.Authorization))
        {
            Console.Error.WriteLine("No session token found. Run 'TaxApiSample login' first, or pass --security/--authorization.");
            return 1;
        }

        if (operation.HasBody && string.IsNullOrWhiteSpace(parsed.Body))
        {
            Console.Error.WriteLine($"Endpoint '{endpointName}' ({method}) requires a request body. Pass --body '<json>' or --body @file.json.");
            return 1;
        }

        using var httpClient = new HttpClient();
        var client = new TaxApiClient(httpClient, options);
        var (statusCode, body) = await client.InvokeAsync(
            endpoint,
            operation,
            parsed.QueryParams,
            parsed.Body,
            integratorKey,
            securityToken,
            parsed.Authorization,
            CancellationToken.None);

        Console.WriteLine($"HTTP {statusCode}");
        Console.WriteLine(body);
        return statusCode is >= 200 and < 300 ? 0 : 1;
    }

    private static ParsedArgs ParseArgs(string[] args)
    {
        string? method = null;
        string? body = null;
        string? security = null;
        string? authorization = null;
        string? integratorKey = null;
        var queryParams = new List<(string Key, string Value)>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--method":
                    method = RequireValue(args, ref i, "--method").ToUpperInvariant();
                    break;
                case "--query":
                    var kvp = RequireValue(args, ref i, "--query");
                    var separatorIndex = kvp.IndexOf('=');
                    if (separatorIndex < 0)
                    {
                        throw new ArgumentException($"--query value '{kvp}' must be in key=value form.");
                    }

                    queryParams.Add((kvp[..separatorIndex], kvp[(separatorIndex + 1)..]));
                    break;
                case "--body":
                    var rawBody = RequireValue(args, ref i, "--body");
                    body = rawBody.StartsWith('@') ? File.ReadAllText(rawBody[1..]) : rawBody;
                    break;
                case "--security":
                    security = RequireValue(args, ref i, "--security");
                    break;
                case "--authorization":
                    authorization = RequireValue(args, ref i, "--authorization");
                    break;
                case "--integrator-key":
                    integratorKey = RequireValue(args, ref i, "--integrator-key");
                    break;
                default:
                    throw new ArgumentException($"Unrecognized argument '{args[i]}'.");
            }
        }

        return new ParsedArgs(method, queryParams, body, security, authorization, integratorKey);
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {flag}.");
        }

        return args[++i];
    }

    private sealed record ParsedArgs(
        string? Method,
        List<(string Key, string Value)> QueryParams,
        string? Body,
        string? Security,
        string? Authorization,
        string? IntegratorKey);
}
