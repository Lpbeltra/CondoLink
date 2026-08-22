using CondoLink.Api.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace CondoLink.Tests;

public sealed class AppExceptionHandlerTests
{
    [Theory]
    [InlineData(typeof(NotFoundAppException), StatusCodes.Status404NotFound)]
    [InlineData(typeof(ForbiddenAppException), StatusCodes.Status403Forbidden)]
    [InlineData(typeof(ConflictAppException), StatusCodes.Status409Conflict)]
    [InlineData(typeof(UnauthorizedAppException), StatusCodes.Status401Unauthorized)]
    public async Task Maps_known_exceptions_to_expected_status(
        Type exceptionType, int expectedStatus)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "boom")!;
        var (status, body) = await HandleAsync(exception, isDevelopment: true);

        Assert.Equal(expectedStatus, status);
        Assert.Contains("boom", body);
    }

    [Fact]
    public async Task Maps_validation_exception_to_bad_request_with_errors()
    {
        var exception = new ValidationAppException(
            "invalid", new Dictionary<string, string[]> { ["pageSize"] = ["must be positive"] });

        var (status, body) = await HandleAsync(exception, isDevelopment: true);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("must be positive", body);
    }

    [Fact]
    public async Task Hides_exception_message_for_unhandled_exceptions_outside_development()
    {
        var (status, body) = await HandleAsync(
            new InvalidOperationException("sensitive internal detail"),
            isDevelopment: false);

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("sensitive internal detail", body);
    }

    [Fact]
    public async Task Includes_exception_message_for_unhandled_exceptions_in_development()
    {
        var (status, body) = await HandleAsync(
            new InvalidOperationException("dev detail"),
            isDevelopment: true);

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Contains("dev detail", body);
    }

    private static async Task<(int Status, string Body)> HandleAsync(
        Exception exception, bool isDevelopment)
    {
        var handler = new AppExceptionHandler(
            new FakeHostEnvironment(isDevelopment),
            NullLogger<AppExceptionHandler>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private sealed class FakeHostEnvironment(bool isDevelopment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isDevelopment ? "Development" : "Production";
        public string ApplicationName { get; set; } = "CondoLink.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
