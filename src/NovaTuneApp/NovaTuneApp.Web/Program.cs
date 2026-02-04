using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Serve admin app from /admin path
var adminPath = Path.Combine(builder.Environment.WebRootPath, "admin");
if (Directory.Exists(adminPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(adminPath),
        RequestPath = "/admin"
    });
}

// SPA fallback for player app (root)
app.MapFallbackToFile("index.html");

// SPA fallback for admin routes
app.MapFallback("/admin/{**path}", async context =>
{
    context.Response.ContentType = "text/html";
    var indexPath = Path.Combine(builder.Environment.WebRootPath, "admin", "index.html");
    if (File.Exists(indexPath))
    {
        await context.Response.SendFileAsync(indexPath);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Admin app not found. Run 'pnpm build' in NovaTuneClient first.");
    }
});

app.MapDefaultEndpoints();

app.Run();
