using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Authorization;
using NovaTuneApp.ApiService.Infrastructure;
using NovaTuneApp.ApiService.Models.Upload;
using NovaTuneApp.ApiService.Services;

namespace NovaTuneApp.ApiService.Endpoints;

/// <summary>
/// Upload endpoints for direct-to-storage track uploads (Stage 2).
/// </summary>
public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tracks/upload")
            .WithTags("Uploads")
            .WithOpenApi()
            .RequireAuthorization(PolicyNames.ActiveUser)
            .AddEndpointFilter<UploadExceptionFilter>();

        group.MapPost("/initiate", InitiateUpload)
            .WithName("InitiateUpload")
            .WithDescription("Request a presigned URL for direct upload to storage")
            .Produces<InitiateUploadResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting("upload-initiate");
    }

    private static async Task<IResult> InitiateUpload(
        [FromBody] InitiateUploadRequest request,
        ClaimsPrincipal user,
        IUploadService uploadService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Unauthorized();
        }

        var response = await uploadService.InitiateUploadAsync(userId, request, ct);
        return TypedResults.Ok(response);
    }
}
