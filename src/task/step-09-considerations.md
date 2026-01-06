# Step 9: Considerations

## 1. Test Execution Time

**Impact**: Real containers are slower than mocks (~30-60s startup per test class)

**Mitigations**:
- Keep test collection sequential to share containers across tests
- Consider using xUnit class fixtures to share factory across tests in same class
- Use `[Trait("Category", "Aspire")]` to allow filtering in CI

```csharp
[Trait("Category", "Aspire")]
[Collection("Auth Integration Tests")]
public class AuthIntegrationTests : IAsyncLifetime
```

## 2. Resource Cleanup

**Risk**: Containers/databases may not be cleaned up on test failure

**Mitigations**:
- Implement `IAsyncDisposable` pattern correctly
- Use try/finally in `DisposeAsync`
- Consider implementing cleanup in a separate finally block

```csharp
public async Task DisposeAsync()
{
    try
    {
        _documentStore?.Dispose();
        _client?.Dispose();
    }
    finally
    {
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }
}
```

## 3. CI/CD Requirements

**Requirement**: Docker must be available in test environment

**Actions**:
- Ensure CI runners have Docker installed
- Add Docker health check to CI pipeline
- Consider skipping Aspire tests if Docker unavailable

```yaml
# Example GitHub Actions
- name: Check Docker
  run: docker --version

- name: Run Integration Tests
  run: dotnet test --filter "Category=Aspire"
```

## 4. Parallel Execution

**Issue**: Keep sequential execution to avoid port/resource conflicts

**Solution**: Maintain `DisableParallelization = true` in collection definition:

```csharp
[CollectionDefinition("Auth Integration Tests", DisableParallelization = true)]
public class AuthIntegrationTestCollection { }
```

## 5. Rate Limiting

**Requirement**: Maintain high rate limits in test configuration

**Implementation**: Already handled in factory configuration:
```csharp
["RateLimiting:Auth:LoginPerIp:PermitLimit"] = "1000",
["RateLimiting:Auth:LoginPerIp:WindowMinutes"] = "1",
// ... etc
```

## 6. Serilog Handling

**Issue**: May still need bootstrap logger handling due to container startup

**Solution**: Keep Serilog initialization in factory constructor if needed:

```csharp
public AspireAuthApiFactory()
{
    // Reset Serilog before creating the factory to avoid disposed provider references
    Log.CloseAndFlush();
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .WriteTo.Console()
        .CreateLogger();
}
```

## 7. Connection String Format

**Issue**: RavenDB connection string from Aspire may have different format

**Verification**: Test the actual connection string format:
```csharp
var connectionString = await _app.GetConnectionStringAsync("novatune");
Console.WriteLine($"RavenDB connection: {connectionString}");
```

Expected format: `http://localhost:XXXXX` (dynamic port)

## 8. Database Name

**Issue**: Database name must match what AppHost creates

**Verification**: Check AppHost configuration:
```csharp
var database = ravenServer.AddDatabase("novatune");
```

Use `"NovaTune"` or the actual database name in DocumentStore configuration.

## Acceptance Criteria

- [ ] Test execution time is acceptable for CI
- [ ] Resources are properly cleaned up on success and failure
- [ ] CI pipeline has Docker available
- [ ] Sequential execution is enforced
- [ ] Rate limits don't interfere with tests
- [ ] Serilog doesn't cause conflicts
