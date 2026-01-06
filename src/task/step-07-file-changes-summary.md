# Step 7: File Changes Summary

## Files to Modify

| File | Action | Description |
|------|--------|-------------|
| `AuthApiFactory.cs` | **Rewrite** | Replace with Aspire.Hosting.Testing implementation |
| `AuthIntegrationTests.cs` | **Update** | Change helper method calls to async versions |
| `TestCollections.cs` | **Keep** | No changes needed, sequential execution still required |
| `NovaTuneApp.IntegrationTests.csproj` | **Verify** | Ensure RavenDB.Client reference is present |

## Detailed Changes

### AuthApiFactory.cs

**Remove:**
- `WebApplicationFactory<Program>` base class
- In-memory `Dictionary<string, ApplicationUser> _users`
- In-memory `Dictionary<string, RefreshToken> _tokens`
- All NSubstitute mock creation
- `CreateInMemoryUserStore()` method
- `CreateInMemoryRefreshTokenRepository()` method
- `ConfigureWebHost()` override
- `CreateHost()` override
- Synchronous helper methods

**Add:**
- `IAsyncLifetime` interface implementation
- `DistributedApplication _app` field
- `IDocumentStore _documentStore` field
- `InitializeAsync()` with Aspire setup
- `DisposeAsync()` with cleanup
- Async helper methods (`GetUserByEmailAsync`, `UpdateUserStatusAsync`, etc.)

### AuthIntegrationTests.cs

**Update:**
- `_factory.GetUserByEmail(email)` → `await _factory.GetUserByEmailAsync(email)`
- `user.Status = UserStatus.Disabled` → `await _factory.UpdateUserStatusAsync(email, UserStatus.Disabled)`
- `_factory.CreateClient()` → `_factory.Client`

### NovaTuneApp.IntegrationTests.csproj

**Verify/Add:**
```xml
<PackageReference Include="RavenDB.Client" Version="6.x.x" />
```

(Check if already transitively included via ApiService reference)

## Files Not Changed

- `TestCollections.cs` - Sequential execution still needed for container resources
- `WebTests.cs` - Separate test file, not affected

## Acceptance Criteria

- [ ] All file changes identified and documented
- [ ] No breaking changes to test collection configuration
- [ ] All necessary package references in place
