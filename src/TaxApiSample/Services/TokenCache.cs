using System.Text.Json;

namespace TaxApiSample.Services;

/// <summary>
/// Persists the session token returned by "login" so subsequent commands
/// don't need to re-authenticate. Stored outside the repo, under the user's
/// local application data folder.
/// </summary>
public sealed class TokenCache
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaxApiSample",
        "token.json");

    public void Save(string token)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new StoredToken(token, DateTimeOffset.UtcNow)));
    }

    public string? Load()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        var stored = JsonSerializer.Deserialize<StoredToken>(File.ReadAllText(FilePath));
        return stored?.Token;
    }

    public void Clear()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }

    private sealed record StoredToken(string Token, DateTimeOffset SavedAtUtc);
}
