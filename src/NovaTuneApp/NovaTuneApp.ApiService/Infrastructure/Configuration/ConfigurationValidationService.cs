using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// Hosted service that validates configuration at startup.
/// Implements fail-fast behavior per NF-5.1.
/// </summary>
public class ConfigurationValidationService : IHostedService
{
    private readonly IOptions<NovaTuneOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ConfigurationValidationService> _logger;

    public ConfigurationValidationService(
        IOptions<NovaTuneOptions> options,
        IHostEnvironment environment,
        ILogger<ConfigurationValidationService> logger)
    {
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var errors = new List<string>();

        // Validate using DataAnnotations
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(options, validationContext, validationResults, validateAllProperties: true))
        {
            errors.AddRange(validationResults.Select(r => r.ErrorMessage ?? "Validation error"));
        }

        // Validate nested objects
        ValidateNestedOptions(options.PresignedUrl, "PresignedUrl", errors);
        ValidateNestedOptions(options.RateLimit, "RateLimit", errors);
        ValidateNestedOptions(options.Quotas, "Quotas", errors);

        // Custom validation: TopicPrefix matches environment
        ValidateTopicPrefix(options, errors);

        // Custom validation: Presigned URL TTL ≤ 1 hour
        ValidatePresignedUrlTtl(options, errors);

        // Custom validation: Cache encryption for non-dev environments
        ValidateCacheEncryption(options, errors);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                _logger.LogError("Configuration validation failed: {Error}", error);
            }

            throw new InvalidOperationException(
                $"Configuration validation failed with {errors.Count} error(s). " +
                $"See logs for details. Errors: {string.Join("; ", errors)}");
        }

        _logger.LogInformation(
            "Configuration validated successfully. TopicPrefix: {TopicPrefix}, Environment: {Environment}",
            options.TopicPrefix,
            _environment.EnvironmentName);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ValidateNestedOptions<T>(T options, string name, List<string> errors) where T : class
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(options, context, results, validateAllProperties: true))
        {
            errors.AddRange(results.Select(r => $"{name}: {r.ErrorMessage}"));
        }
    }

    private void ValidateTopicPrefix(NovaTuneOptions options, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(options.TopicPrefix))
        {
            errors.Add("TopicPrefix cannot be empty or whitespace. Set NovaTune:TopicPrefix in configuration.");
            return;
        }

        // Validate that TopicPrefix follows Kafka topic naming conventions
        var validChars = options.TopicPrefix.All(c =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');

        if (!validChars)
        {
            errors.Add(
                $"TopicPrefix '{options.TopicPrefix}' contains invalid characters. " +
                "Only letters, digits, hyphens, underscores, and dots are allowed.");
        }

        // Warn if TopicPrefix doesn't match environment
        var envLower = _environment.EnvironmentName.ToLowerInvariant();
        var prefixLower = options.TopicPrefix.ToLowerInvariant();

        if (!prefixLower.Contains(envLower) && !envLower.Contains(prefixLower))
        {
            _logger.LogWarning(
                "TopicPrefix '{TopicPrefix}' does not contain environment name '{Environment}'. " +
                "Consider using environment-specific topic prefixes for clarity.",
                options.TopicPrefix,
                _environment.EnvironmentName);
        }
    }

    private void ValidatePresignedUrlTtl(NovaTuneOptions options, List<string> errors)
    {
        if (options.PresignedUrl.TtlSeconds <= 0)
        {
            errors.Add(
                $"PresignedUrl.TtlSeconds must be positive. Current value: {options.PresignedUrl.TtlSeconds}");
        }
        else if (options.PresignedUrl.TtlSeconds > 3600)
        {
            errors.Add(
                $"PresignedUrl.TtlSeconds cannot exceed 1 hour (3600 seconds). Current value: {options.PresignedUrl.TtlSeconds}");
        }
    }

    private void ValidateCacheEncryption(NovaTuneOptions options, List<string> errors)
    {
        // Skip validation if encryption is disabled
        if (!options.CacheEncryption.Enabled)
        {
            if (!_environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "Cache encryption is disabled in {Environment} environment. " +
                    "Consider enabling encryption for production deployments.",
                    _environment.EnvironmentName);
            }
            return;
        }

        // Encryption is enabled - validate key
        if (string.IsNullOrWhiteSpace(options.CacheEncryption.Key))
        {
            errors.Add(
                "CacheEncryption.Key is required when encryption is enabled. " +
                $"Set NovaTune:CacheEncryption:Key with at least {CacheEncryptionOptions.MinimumKeyLength} characters.");
            return;
        }

        if (options.CacheEncryption.Key.Length < CacheEncryptionOptions.MinimumKeyLength)
        {
            errors.Add(
                $"CacheEncryption.Key must be at least {CacheEncryptionOptions.MinimumKeyLength} characters. " +
                $"Current length: {options.CacheEncryption.Key.Length}");
        }

        // Check for minimum entropy (no repeated characters)
        var uniqueChars = options.CacheEncryption.Key.Distinct().Count();
        var minUniqueChars = CacheEncryptionOptions.MinimumKeyLength / 4;

        if (uniqueChars < minUniqueChars)
        {
            errors.Add(
                $"CacheEncryption.Key has insufficient entropy. " +
                $"Key has {uniqueChars} unique characters, minimum recommended: {minUniqueChars}");
        }
    }
}
