using System.Text.Json;
using MyCCHTools.Configuration;
using MyCCHTools.Services;
using Spectre.Console;

var tokenCache = new TokenCache();
var loginAttempt = 0;

try
{
    var baseUrl = LoadBaseUrl();
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    var authenticationClient = new AuthenticationClient(httpClient);

    while (tokenCache.Get() is null)
    {
        try
        {
            var configuration = EnvironmentConfiguration.Load(baseUrl, loginAttempt > 0);
            var result = await authenticationClient.AuthenticateAsync(configuration, CancellationToken.None);
            tokenCache.Store(result.Token, result.ExpiresAt);
            AnsiConsole.MarkupLine("[green]Login successful.[/] The token is cached for this session.");
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]The operation was cancelled.[/]");
        }
        catch (Exception exception)
        {
            AnsiConsole.MarkupLine($"[red]Login failed:[/] {Markup.Escape(exception.Message)}");
        }

        if (tokenCache.Get() is not null)
        {
            break;
        }

        loginAttempt++;
        var retry = AnsiConsole.Confirm("Try the login again?", defaultValue: true);
        if (!retry)
        {
            return 0;
        }
    }

    await ShowActionMenuAsync(tokenCache);
    return 0;
}
catch (Exception exception)
{
    AnsiConsole.MarkupLine($"[red]Application error:[/] {Markup.Escape(exception.Message)}");
    return 1;
}

static async Task ShowActionMenuAsync(TokenCache tokenCache)
{
    while (tokenCache.Get() is not null)
    {
        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose an action")
                .AddChoices("Test action 1", "Test action 2", "Show cache status", "Exit"));

        switch (action)
        {
            case "Test action 1":
            case "Test action 2":
                AnsiConsole.MarkupLine($"[yellow]{action} selected.[/] No service action is wired yet.");
                break;
            case "Show cache status":
                AnsiConsole.MarkupLine(tokenCache.Get() is null
                    ? "[red]The cached token has expired.[/]"
                    : "[green]A valid token is cached.[/]");
                break;
            case "Exit":
                return;
        }
    }

    AnsiConsole.MarkupLine("[yellow]The cached token has expired. Please run the tool again to log in.[/]");
    await Task.CompletedTask;
}

static string LoadBaseUrl()
{
    var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    if (!File.Exists(path))
    {
        return "https://test4api.cchaxcess.com/api/AuthService";
    }

    using var document = JsonDocument.Parse(File.ReadAllText(path));
    return document.RootElement.GetProperty("AuthenticationBaseUrl").GetString()
        ?? throw new InvalidOperationException("AuthenticationBaseUrl is missing from appsettings.json.");
}