using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NovaTuneApp.ApiService.Authorization;
using NovaTuneApp.ApiService.Models;

namespace NovaTune.UnitTests.Auth;

/// <summary>
/// Unit tests for authorization policies and status enforcement (Req 1.3, 1.4).
/// Tests ActiveUserRequirement and CanStreamRequirement handlers.
/// </summary>
public class AuthorizationPolicyTests
{
    // ========================================================================
    // ActiveUserRequirement Tests
    // ========================================================================

    [Fact]
    public async Task ActiveUserHandler_Should_succeed_for_active_user()
    {
        var handler = new ActiveUserHandler();
        var user = CreateClaimsPrincipal(UserStatus.Active);
        var context = CreateAuthorizationContext(user, new ActiveUserRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ActiveUserHandler_Should_fail_for_disabled_user()
    {
        var handler = new ActiveUserHandler();
        var user = CreateClaimsPrincipal(UserStatus.Disabled);
        var context = CreateAuthorizationContext(user, new ActiveUserRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task ActiveUserHandler_Should_fail_for_pending_deletion_user()
    {
        var handler = new ActiveUserHandler();
        var user = CreateClaimsPrincipal(UserStatus.PendingDeletion);
        var context = CreateAuthorizationContext(user, new ActiveUserRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task ActiveUserHandler_Should_fail_when_no_status_claim()
    {
        var handler = new ActiveUserHandler();
        var user = CreateClaimsPrincipalWithoutStatus();
        var context = CreateAuthorizationContext(user, new ActiveUserRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    // ========================================================================
    // CanStreamRequirement Tests
    // ========================================================================

    [Fact]
    public async Task CanStreamHandler_Should_succeed_for_active_user()
    {
        var handler = new CanStreamHandler();
        var user = CreateClaimsPrincipal(UserStatus.Active);
        var context = CreateAuthorizationContext(user, new CanStreamRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task CanStreamHandler_Should_succeed_for_pending_deletion_user()
    {
        var handler = new CanStreamHandler();
        var user = CreateClaimsPrincipal(UserStatus.PendingDeletion);
        var context = CreateAuthorizationContext(user, new CanStreamRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task CanStreamHandler_Should_fail_for_disabled_user()
    {
        var handler = new CanStreamHandler();
        var user = CreateClaimsPrincipal(UserStatus.Disabled);
        var context = CreateAuthorizationContext(user, new CanStreamRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task CanStreamHandler_Should_fail_when_no_status_claim()
    {
        var handler = new CanStreamHandler();
        var user = CreateClaimsPrincipalWithoutStatus();
        var context = CreateAuthorizationContext(user, new CanStreamRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    // ========================================================================
    // Requirement Instance Tests
    // ========================================================================

    [Fact]
    public void ActiveUserRequirement_Should_implement_IAuthorizationRequirement()
    {
        var requirement = new ActiveUserRequirement();

        requirement.ShouldBeAssignableTo<IAuthorizationRequirement>();
    }

    [Fact]
    public void CanStreamRequirement_Should_implement_IAuthorizationRequirement()
    {
        var requirement = new CanStreamRequirement();

        requirement.ShouldBeAssignableTo<IAuthorizationRequirement>();
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static ClaimsPrincipal CreateClaimsPrincipal(UserStatus status)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-id"),
            new(ClaimTypes.Email, "test@example.com"),
            new("status", status.ToString()),
            new("roles", "listener")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateClaimsPrincipalWithoutStatus()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-id"),
            new(ClaimTypes.Email, "test@example.com"),
            new("roles", "listener")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static AuthorizationHandlerContext CreateAuthorizationContext(
        ClaimsPrincipal user,
        IAuthorizationRequirement requirement)
    {
        return new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            resource: null);
    }
}
