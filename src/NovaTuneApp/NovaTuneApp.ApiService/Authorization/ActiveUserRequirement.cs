using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NovaTuneApp.ApiService.Models;

namespace NovaTuneApp.ApiService.Authorization;

/// <summary>
/// Authorization requirement for users with Active status (Req 1.3).
/// </summary>
public class ActiveUserRequirement : IAuthorizationRequirement { }

/// <summary>
/// Handler for ActiveUserRequirement.
/// </summary>
public class ActiveUserHandler : AuthorizationHandler<ActiveUserRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveUserRequirement requirement)
    {
        var statusClaim = context.User.FindFirstValue("status");

        if (statusClaim == UserStatus.Active.ToString())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Authorization requirement for users who can stream (Active or PendingDeletion).
/// </summary>
public class CanStreamRequirement : IAuthorizationRequirement { }

/// <summary>
/// Handler for CanStreamRequirement.
/// PendingDeletion users can still stream per Req 1.3.
/// </summary>
public class CanStreamHandler : AuthorizationHandler<CanStreamRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CanStreamRequirement requirement)
    {
        var statusClaim = context.User.FindFirstValue("status");

        if (statusClaim == UserStatus.Active.ToString() ||
            statusClaim == UserStatus.PendingDeletion.ToString())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
