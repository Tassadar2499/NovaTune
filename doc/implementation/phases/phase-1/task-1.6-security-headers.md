# Task 1.6: HTTP Security Headers

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P1 (Must-have)
> **Status:** Pending
> **NFR Reference:** NF-3.6

## Description

Implement security headers middleware (NF-3.6).

---

## Subtasks

### 1.6.1 Create Security Headers Middleware

- [ ] Create `Infrastructure/SecurityHeadersMiddleware.cs`:

```csharp
namespace NovaTuneApp.ApiService.Infrastructure;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // HSTS - only in production over HTTPS
        if (_environment.IsProduction() && context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] =
                "max-age=31536000; includeSubDomains; preload";
        }

        // Content Security Policy
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'";

        // Prevent clickjacking
        headers["X-Frame-Options"] = "DENY";

        // Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Referrer Policy
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions Policy (formerly Feature-Policy)
        headers["Permissions-Policy"] =
            "geolocation=(), " +
            "microphone=(), " +
            "camera=(), " +
            "payment=(), " +
            "usb=()";

        // Cross-Origin policies
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
```

---

### 1.6.2 Configure HSTS for Production Only

- [ ] Configure HSTS to only apply in production

**Update middleware to use options pattern:**
```csharp
public class SecurityHeadersOptions
{
    public bool EnableHsts { get; set; } = true;
    public int HstsMaxAgeSeconds { get; set; } = 31536000; // 1 year
    public bool HstsIncludeSubDomains { get; set; } = true;
    public bool HstsPreload { get; set; } = true;
    public string? ContentSecurityPolicy { get; set; }
}
```

**Configure in `appsettings.json`:**
```json
{
  "SecurityHeaders": {
    "EnableHsts": true,
    "HstsMaxAgeSeconds": 31536000,
    "HstsIncludeSubDomains": true,
    "HstsPreload": true
  }
}
```

**Configure in `appsettings.Development.json`:**
```json
{
  "SecurityHeaders": {
    "EnableHsts": false
  }
}
```

---

### 1.6.3 Add CSP Configuration Options

- [ ] Add CSP configuration options for different environments

**Extended options:**
```csharp
public class SecurityHeadersOptions
{
    public bool EnableHsts { get; set; } = true;
    public int HstsMaxAgeSeconds { get; set; } = 31536000;
    public bool HstsIncludeSubDomains { get; set; } = true;
    public bool HstsPreload { get; set; } = true;

    public ContentSecurityPolicyOptions Csp { get; set; } = new();
}

public class ContentSecurityPolicyOptions
{
    public string DefaultSrc { get; set; } = "'self'";
    public string ScriptSrc { get; set; } = "'self'";
    public string StyleSrc { get; set; } = "'self' 'unsafe-inline'";
    public string ImgSrc { get; set; } = "'self' data: https:";
    public string FontSrc { get; set; } = "'self'";
    public string ConnectSrc { get; set; } = "'self'";
    public string FrameAncestors { get; set; } = "'none'";
    public string BaseUri { get; set; } = "'self'";
    public string FormAction { get; set; } = "'self'";
    public string[]? ExtraDirectives { get; set; }

    public string Build()
    {
        var directives = new List<string>
        {
            $"default-src {DefaultSrc}",
            $"script-src {ScriptSrc}",
            $"style-src {StyleSrc}",
            $"img-src {ImgSrc}",
            $"font-src {FontSrc}",
            $"connect-src {ConnectSrc}",
            $"frame-ancestors {FrameAncestors}",
            $"base-uri {BaseUri}",
            $"form-action {FormAction}"
        };

        if (ExtraDirectives is not null)
        {
            directives.AddRange(ExtraDirectives);
        }

        return string.Join("; ", directives);
    }
}
```

**Development CSP (more permissive):**
```json
{
  "SecurityHeaders": {
    "Csp": {
      "ScriptSrc": "'self' 'unsafe-inline' 'unsafe-eval'",
      "StyleSrc": "'self' 'unsafe-inline'",
      "ConnectSrc": "'self' ws: wss: http://localhost:* https://localhost:*"
    }
  }
}
```

---

### 1.6.4 Write Tests for Security Headers

- [ ] Create `NovaTuneApp.Tests/Integration/SecurityHeadersTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;

namespace NovaTuneApp.Tests.Integration;

public class SecurityHeadersTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityHeadersTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Response_ContainsXFrameOptions()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
    }

    [Fact]
    public async Task Response_ContainsXContentTypeOptions()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
    }

    [Fact]
    public async Task Response_ContainsContentSecurityPolicy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        var csp = response.Headers.GetValues("Content-Security-Policy").First();
        Assert.Contains("default-src 'self'", csp);
    }

    [Fact]
    public async Task Response_ContainsReferrerPolicy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.Equal(
            "strict-origin-when-cross-origin",
            response.Headers.GetValues("Referrer-Policy").First());
    }

    [Fact]
    public async Task Response_ContainsPermissionsPolicy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.Contains("Permissions-Policy"));
        var policy = response.Headers.GetValues("Permissions-Policy").First();
        Assert.Contains("geolocation=()", policy);
        Assert.Contains("camera=()", policy);
    }

    [Fact]
    public async Task Response_DoesNotContainHstsInDevelopment()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        // HSTS should not be present in development/test environment
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }
}
```

---

## Register Middleware

**In `Program.cs`:**
```csharp
var app = builder.Build();

// Security headers should be early in the pipeline
app.UseSecurityHeaders();

// HTTPS redirection (built-in, handles HSTS in production)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

// ... rest of middleware
```

---

## Security Headers Reference

| Header | Value | Purpose |
|--------|-------|---------|
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains; preload` | Force HTTPS (production only) |
| `Content-Security-Policy` | `default-src 'self'; ...` | Prevent XSS attacks |
| `X-Frame-Options` | `DENY` | Prevent clickjacking |
| `X-Content-Type-Options` | `nosniff` | Prevent MIME sniffing |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Control referrer info |
| `Permissions-Policy` | `geolocation=(), camera=(), ...` | Disable browser features |
| `Cross-Origin-Opener-Policy` | `same-origin` | Isolate browsing context |
| `Cross-Origin-Resource-Policy` | `same-origin` | Control resource sharing |

---

## Acceptance Criteria

- [ ] All security headers present in responses
- [ ] Headers configurable per environment
- [ ] Tests verify header presence
- [ ] HSTS only enabled in production

---

## Verification Commands

```bash
# Check all security headers
curl -si http://localhost:5000/health | grep -E "^(X-|Content-Security|Strict-Transport|Referrer|Permissions|Cross-Origin)"

# Verify X-Frame-Options
curl -si http://localhost:5000/health | grep "X-Frame-Options"

# Verify Content-Security-Policy
curl -si http://localhost:5000/health | grep "Content-Security-Policy"
```

---

## File Checklist

- [ ] `Infrastructure/SecurityHeadersMiddleware.cs`
- [ ] `Infrastructure/SecurityHeadersOptions.cs`
- [ ] `appsettings.json` (updated with SecurityHeaders section)
- [ ] `appsettings.Development.json` (updated with development CSP)
- [ ] `NovaTuneApp.Tests/Integration/SecurityHeadersTests.cs`

---

## Navigation

[Task 1.5: ServiceDefaults](task-1.5-service-defaults.md) | [Phase 1 Overview](overview.md) | [Task 1.7: API Foundation](task-1.7-api-foundation.md)
