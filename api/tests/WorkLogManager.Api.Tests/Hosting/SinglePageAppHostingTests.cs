using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WorkLogManager.Api.Tests.Hosting;

public class SinglePageAppHostingTests : IDisposable
{
    private readonly string _contentRootPath;
    private readonly string _databasePath;
    private readonly SinglePageAppWebApplicationFactory _factory;

    public SinglePageAppHostingTests()
    {
        _contentRootPath = Directory.CreateTempSubdirectory("worklogmanager-api-tests-").FullName;
        var wwwrootPath = Path.Combine(_contentRootPath, "wwwroot");
        Directory.CreateDirectory(wwwrootPath);
        File.WriteAllText(Path.Combine(wwwrootPath, "index.html"), "<html><body>stub</body></html>");

        _databasePath = Path.Combine(Path.GetTempPath(), $"worklogmanager-api-tests-{Guid.NewGuid():N}.db");

        _factory = new SinglePageAppWebApplicationFactory(_contentRootPath, _databasePath);
    }

    [Fact]
    public async Task Get_ExistingApiEndpoint_ReturnsJson()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/employees");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_NonApiRouteWithoutMatchingFile_FallsBackToIndexHtml()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/employees-page/some-client-side-route");

        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("stub", body);
    }

    public void Dispose()
    {
        _factory.Dispose();

        if (Directory.Exists(_contentRootPath))
        {
            Directory.Delete(_contentRootPath, recursive: true);
        }

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private sealed class SinglePageAppWebApplicationFactory(string contentRootPath, string databasePath)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(contentRootPath);

            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:WorkLogManagerDb"] = $"Data Source={databasePath}",
                });
            });
        }
    }
}
