using Serilog.Core;
using Serilog.Events;

namespace NovaTuneApp.ApiService.Infrastructure.Logging;

/// <summary>
/// Serilog destructuring policy that redacts sensitive values from logs.
/// Prevents accidental logging of passwords, tokens, presigned URLs, and other secrets.
/// </summary>
public class RedactedDestructuringPolicy : IDestructuringPolicy
{
    private const string RedactedValue = "[REDACTED]";

    /// <summary>
    /// Property names that should always be redacted when logged as object properties.
    /// </summary>
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwd",
        "pwd",
        "token",
        "accesstoken",
        "access_token",
        "refreshtoken",
        "refresh_token",
        "secret",
        "secretkey",
        "secret_key",
        "apikey",
        "api_key",
        "authorization",
        "bearer",
        "credential",
        "credentials",
        "connectionstring",
        "connection_string",
        "presignedurl",
        "presigned_url",
        "signedurl",
        "signed_url",
        "objectkey",
        "object_key",
        "accesskey",
        "access_key",
        "saslpassword",
        "sasl_password"
    };

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        // Handle string values that look like sensitive data
        if (value is string stringValue && LooksLikeSensitiveValue(stringValue))
        {
            result = new ScalarValue(RedactedValue);
            return true;
        }

        result = null!;
        return false;
    }

    /// <summary>
    /// Determines if a string value looks like it might be sensitive data.
    /// </summary>
    private static bool LooksLikeSensitiveValue(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 10)
        {
            return false;
        }

        // Detect AWS presigned URLs
        if (value.Contains("X-Amz-Signature", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("X-Amz-Credential", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Detect URLs with token/key query parameters
        if (value.Contains("://") &&
            (value.Contains("token=", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("key=", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("signature=", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Detect JWT tokens (header.payload.signature format)
        if (value.StartsWith("eyJ", StringComparison.Ordinal) && value.Count(c => c == '.') == 2)
        {
            return true;
        }

        // Detect Bearer tokens
        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a property name indicates sensitive data.
    /// Can be used by custom enrichers or log formatters.
    /// </summary>
    public static bool IsSensitivePropertyName(string propertyName)
    {
        return SensitivePropertyNames.Contains(propertyName);
    }

    /// <summary>
    /// Returns a redacted version of the value if the property name is sensitive.
    /// </summary>
    public static string RedactIfSensitive(string propertyName, string? value)
    {
        if (IsSensitivePropertyName(propertyName) || (value is not null && LooksLikeSensitiveValue(value)))
        {
            return RedactedValue;
        }

        return value ?? string.Empty;
    }
}

/// <summary>
/// Serilog enricher that masks sensitive properties in structured log events.
/// </summary>
public class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var propertiesToUpdate = new List<(string Name, LogEventPropertyValue Value)>();

        foreach (var property in logEvent.Properties)
        {
            if (RedactedDestructuringPolicy.IsSensitivePropertyName(property.Key))
            {
                propertiesToUpdate.Add((property.Key, new ScalarValue("[REDACTED]")));
            }
        }

        foreach (var (name, value) in propertiesToUpdate)
        {
            logEvent.AddOrUpdateProperty(new LogEventProperty(name, value));
        }
    }
}
