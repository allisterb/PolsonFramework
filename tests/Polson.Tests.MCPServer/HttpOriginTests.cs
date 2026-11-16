namespace Polson.Tests.MCPServer;

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// The HTTP transport answers this machine only. It runs scripts and writes files without authentication, and until
/// 2026-11-15 it allowed any CORS origin, so any web page could drive it; a DNS-rebinding page still could, carrying its
/// own name in the Host header.
/// </summary>
public class HttpOriginTests : TestsRuntime, IAsyncLifetime
{
    #region Fields
    private WebApplication app = null!;
    private Uri baseUrl = null!;
    #endregion

    #region Methods
    public async Task InitializeAsync()
    {
        app = PolsonMCPServer.BuildHttpApp(config);
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        baseUrl = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First());
    }

    public async Task DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private async Task<HttpResponseMessage> Preflight(string origin)
    {
        using var http = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Options, baseUrl);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await http.SendAsync(request);
    }
    #endregion

    #region Tests
    [Theory]
    [InlineData("http://localhost:6274", true)]
    [InlineData("http://127.0.0.1:3000", true)]
    [InlineData("http://[::1]:8080", true)]
    [InlineData("https://evil.example", false)]
    [InlineData("http://localhost.evil.example", false)]
    [InlineData("null", false)]
    public void TestOnlyALoopbackOriginIsAllowed(string origin, bool allowed) =>
        Assert.Equal(allowed, PolsonMCPServer.IsLoopbackOrigin(origin));

    [Fact]
    public async Task TestAForeignOriginGetsNoCorsGrant()
    {
        using var response = await Preflight("https://evil.example");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task TestALocalOriginIsGranted()
    {
        using var response = await Preflight("http://localhost:6274");

        Assert.Equal("http://localhost:6274", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task TestARebindingHostIsRefused()
    {
        using var http = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, baseUrl);
        request.Headers.Host = "rebind.evil.example";

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    #endregion
}
