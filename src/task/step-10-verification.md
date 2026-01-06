# Step 10: Verification

## Objective

Verify the implementation meets all requirements and tests pass.

## Verification Checklist

### 1. All Tests Pass

Run the test suite:

```bash
dotnet test src/integration_tests/NovaTuneApp.IntegrationTests --filter "Category=Aspire"
```

Expected: All 14 auth integration tests pass

| Test | Expected Result |
|------|-----------------|
| `Register_Should_return_201_with_user_data` | Pass |
| `Register_Should_create_user_with_active_status` | Pass |
| `Register_Should_return_409_for_duplicate_email` | Pass |
| `Login_Should_return_200_with_tokens` | Pass |
| `Login_Should_return_401_for_wrong_password` | Pass |
| `Login_Should_return_401_for_nonexistent_user` | Pass |
| `Login_Should_return_403_for_disabled_user` | Pass |
| `Refresh_Should_rotate_tokens` | Pass |
| `Refresh_Should_return_401_for_invalid_token` | Pass |
| `Refresh_Should_return_401_for_already_used_token` | Pass |
| `Refresh_Should_return_403_for_disabled_user` | Pass |
| `Logout_Should_return_401_without_auth_header` | Pass |
| `Errors_Should_include_traceId` | Pass |
| `Errors_Should_include_instance_path` | Pass |

### 2. No NSubstitute in AuthApiFactory

Verify no mock references remain:

```bash
grep -n "NSubstitute\|Substitute.For\|Arg.Any" src/integration_tests/NovaTuneApp.IntegrationTests/AuthApiFactory.cs
```

Expected: No matches

### 3. No In-Memory Dictionary Storage

Verify no Dictionary stores:

```bash
grep -n "Dictionary<string" src/integration_tests/NovaTuneApp.IntegrationTests/AuthApiFactory.cs
```

Expected: No matches

### 4. Real Data Persists in RavenDB

During test execution, verify:
1. Users are created in RavenDB (check via RavenDB Studio if available)
2. Tokens are stored in RavenDB
3. Data cleanup works correctly

### 5. Containers Start and Stop Cleanly

Check Docker after tests:

```bash
docker ps -a | grep -E "ravendb|redis|kafka|redpanda"
```

Expected: No orphaned containers from test runs

### 6. Code Review Checklist

- [ ] `AuthApiFactory` implements `IAsyncLifetime`
- [ ] Uses `DistributedApplicationTestingBuilder`
- [ ] Creates `HttpClient` via `_app.CreateHttpClient("apiservice")`
- [ ] Connects to RavenDB via connection string from Aspire
- [ ] All helper methods are async
- [ ] Proper disposal of resources
- [ ] Test configuration includes JWT and rate limit settings

### 7. Integration Test Structure

Verify test class structure:

```csharp
[Trait("Category", "Aspire")]
[Collection("Auth Integration Tests")]
public class AuthIntegrationTests : IAsyncLifetime
{
    private AspireAuthApiFactory _factory = null!;
    private HttpClient _client = null!;
    // ...
}
```

## Troubleshooting

### Tests Timeout

- Increase timeout: `[Fact(Timeout = 120000)]`
- Check Docker is running
- Check container health

### Connection Refused

- Verify container is running
- Check connection string format
- Ensure proper wait for readiness

### Data Not Persisting

- Check database name matches
- Verify session.SaveChangesAsync() is called
- Check for transaction issues

### Rate Limit Errors

- Verify configuration is being applied
- Check rate limit settings in factory

## Final Acceptance Criteria

- [ ] All 14 tests pass
- [ ] No NSubstitute references in AuthApiFactory
- [ ] No in-memory Dictionary storage
- [ ] Real data persists in RavenDB during test execution
- [ ] Containers start and stop cleanly
- [ ] No resource leaks after test runs
- [ ] CI pipeline passes (if applicable)
