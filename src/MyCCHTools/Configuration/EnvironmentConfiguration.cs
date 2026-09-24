using Spectre.Console;

namespace MyCCHTools.Configuration;

public sealed record EnvironmentConfiguration(
    string IntegratorKey,
    string UserName,
    string Password,
    string? UserSid,
    string? Realm,
    bool IsInternal,
    string AuthenticationBaseUrl)
{
    public static EnvironmentConfiguration Load(string authenticationBaseUrl, bool promptForCredentials = false)
    {
        LoadDotEnv();

        var integratorKey = Environment.GetEnvironmentVariable("INTEGRATOR_KEY");
        if (string.IsNullOrWhiteSpace(integratorKey))
        {
            throw new InvalidOperationException(
                "Configuration error: INTEGRATOR_KEY is required and cannot be prompted for.");
        }

        var userName = GetOrPrompt("CCH_USERNAME", "CCH username", forcePrompt: promptForCredentials);
        var password = GetOrPrompt("CCH_PASSWORD", "CCH password", true, promptForCredentials);
        var userSid = GetOrPrompt("CCH_USER_SID", "CCH user SID", forcePrompt: promptForCredentials);
        var realm = GetOrPrompt("CCH_REALM", "CCH realm", forcePrompt: promptForCredentials);
        var isInternal = ParseBoolean(Environment.GetEnvironmentVariable("CCH_IS_INTERNAL"), true);

        return new EnvironmentConfiguration(
            integratorKey,
            userName,
            password,
            userSid,
            realm,
            isInternal,
            Environment.GetEnvironmentVariable("CCH_AUTH_BASE_URL") ?? authenticationBaseUrl);
    }

    private static string GetOrPrompt(string key, string label, bool secret = false, bool forcePrompt = false)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (!forcePrompt && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return secret
            ? AnsiConsole.Prompt(new TextPrompt<string>($"{label}:").Secret())
            : AnsiConsole.Ask<string>($"{label}:");
    }

    private static bool ParseBoolean(string? value, bool defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : bool.Parse(value);
    }

    private static void LoadDotEnv()
    {
        var path = FindEnvFile();
        if (path is null)
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim().Trim('"', '\'');
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? FindEnvFile()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, ".env");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}