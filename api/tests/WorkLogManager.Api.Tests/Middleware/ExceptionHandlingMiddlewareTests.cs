using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WorkLogManager.Api.Middleware;

namespace WorkLogManager.Api.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenNextThrowsJsonException_ReturnsBadRequestWithMessage()
    {
        RequestDelegate next = _ => throw new JsonException("Malformed payload details that must not leak.");
        var middleware = new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        Assert.True(document.RootElement.TryGetProperty("message", out var messageProperty));
        Assert.Equal("Invalid request payload.", messageProperty.GetString());
    }
}
