using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using NovaTuneApp.ApiService.Authorization;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Identity;
using NovaTuneApp.ApiService.Models.Identity;
using NovaTuneApp.ApiService.Services;

namespace NovaTuneApp.ApiService.Extensions;

/// <summary>
/// Extension methods for configuring authentication and authorization.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Adds NovaTune authentication services including JWT, Identity, and authorization policies.
    /// </summary>
    public static IHostApplicationBuilder AddNovaTuneAuthentication(this IHostApplicationBuilder builder)
    {
        // Bind configuration sections
        builder.Services.Configure<JwtSettings>(
            builder.Configuration.GetSection(JwtSettings.SectionName));
        builder.Services.Configure<Argon2Settings>(
            builder.Configuration.GetSection(Argon2Settings.SectionName));
        builder.Services.Configure<SessionSettings>(
            builder.Configuration.GetSection(SessionSettings.SectionName));

        // Register JWT key provider as singleton (single source of truth for signing key)
        var keyProvider = new JwtKeyProvider(builder.Configuration);
        builder.Services.AddSingleton<IJwtKeyProvider>(keyProvider);

        // Register identity stores
        builder.Services.AddScoped<IUserStore<ApplicationUser>, RavenDbUserStore>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, Argon2PasswordHasher>();
        builder.Services.AddSingleton<ITokenService, TokenService>();
        builder.Services.AddScoped<IAuthService, AuthService>();

        // Register authorization handlers
        builder.Services.AddScoped<IAuthorizationHandler, ActiveUserHandler>();
        builder.Services.AddScoped<IAuthorizationHandler, CanStreamHandler>();

        // Configure JWT authentication using key provider
        var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings not configured");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Prevent automatic claim type transformation (e.g., "roles" to ClaimTypes.Role)
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyProvider.SigningKey),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub", // Keep original JWT claim type (no transformation)
                    RoleClaimType = "roles" // Role claim type in our JWT
                };
            });

        // Configure authorization policies (Req 1.4)
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyNames.Listener, policy =>
                policy.RequireAuthenticatedUser());

            options.AddPolicy(PolicyNames.Admin, policy =>
                policy.RequireClaim("roles", "admin"));

            options.AddPolicy(PolicyNames.ActiveUser, policy =>
                policy.Requirements.Add(new ActiveUserRequirement()));

            options.AddPolicy(PolicyNames.CanStream, policy =>
                policy.Requirements.Add(new CanStreamRequirement()));

            // Admin with audit access policy (Stage 8)
            options.AddPolicy(PolicyNames.AdminWithAuditAccess, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim("roles", "admin") &&
                    context.User.HasClaim("permissions", "audit.read")));
        });

        return builder;
    }
}
