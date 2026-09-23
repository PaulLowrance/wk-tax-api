namespace MyCCHTools.Services;

public sealed class TokenCache
{
    private string? token;
    private DateTimeOffset expiresAt;

    public void Store(string value, DateTimeOffset? expiresAt = null)
    {
        token = value;
        this.expiresAt = expiresAt ?? DateTimeOffset.MaxValue;
    }

    public string? Get()
    {
        if (token is null || DateTimeOffset.UtcNow >= expiresAt)
        {
            token = null;
            return null;
        }

        return token;
    }
}