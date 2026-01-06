namespace NovaTuneApp.Tests;

/// <summary>
/// Collection to run auth integration tests sequentially.
/// This prevents Serilog frozen logger conflicts between parallel test runs.
/// </summary>
[CollectionDefinition("Auth Integration Tests", DisableParallelization = true)]
public class AuthIntegrationTestCollection { }

/// <summary>
/// Collection to run rate limit tests sequentially.
/// </summary>
[CollectionDefinition("Rate Limit Tests", DisableParallelization = true)]
public class RateLimitTestCollection { }
