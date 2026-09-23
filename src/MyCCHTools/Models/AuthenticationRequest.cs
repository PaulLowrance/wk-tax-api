namespace MyCCHTools.Models;

public sealed record AuthenticationRequest(
    string UserName,
    string? UserSid,
    string Password,
    string? Realm,
    bool IsInternal);