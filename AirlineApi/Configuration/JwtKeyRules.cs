namespace AirlineApi.Configuration;

public static class JwtKeyRules
{
    public const string FailureMessage =
        "Jwt:SigningKey отсутствует или небезопасен. Задайте ключ длиной не меньше 32 символов через user-secrets (Jwt:SigningKey) или переменную окружения Jwt__SigningKey. Пустое значение из appsettings.json не используется как секрет.";

    public static bool IsAcceptable(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return false;
        }

        var trimmed = signingKey.Trim();
        return trimmed.Length >= 32 && !IsPlaceholder(trimmed);
    }

    public static bool IsPlaceholder(string signingKey)
    {
        var normalized = signingKey.Trim().ToLowerInvariant();
        string[] banned =
        [
            "changeme",
            "change_me",
            "change-me",
            "placeholder",
            "dev-secret",
            "your-secret",
            "replace-me",
            "jwt-signing-key",
            "secretkey",
            "supersecret"
        ];

        return banned.Any(normalized.Contains);
    }
}
