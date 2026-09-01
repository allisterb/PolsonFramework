namespace Polson.Tests.MCPServer;

using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// The published symbol surface stops at the SDK's edge.
/// </summary>
/// <remarks>
/// Several JS-reachable toolkits inherit <c>Runtime</c> for logging and configuration. Walking into
/// it published around thirty .NET infrastructure members as SDK calls — <c>Assets.downloadFile</c>,
/// <c>Assets.entryAssembly</c>, and <c>Assets.camelDir</c>, a name left over from another project.
/// <para>
/// <c>Search</c> promises that a <c>direct</c> verdict means the call exists and its signature is
/// authoritative. For those members that promise was false, which is worse than an absent entry: an
/// agent asking about one was told to use it.
/// </para>
/// </remarks>
public class JsSurfaceBoundaryTests : TestsRuntime
{
    #region Methods
    [Theory]
    [InlineData("downloadFile")]
    [InlineData("entryAssembly")]
    [InlineData("assemblyLocation")]
    [InlineData("copyDirectory")]
    [InlineData("camelDir")]
    [InlineData("closeAndFlushAuditLog")]
    [InlineData("failIfNoConfiguration")]
    public void TestRuntimeInfrastructureIsNotPublishedAsAnSdkCall(string member)
    {
        var published = JsSymbolManifest.Symbols
            .Where(s => s.Name.EndsWith("." + member, System.StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Name)
            .ToArray();

        Assert.True(published.Length == 0,
            $"'{member}' is Runtime infrastructure and is published as: {string.Join(", ", published)}");
    }

    /// <summary>The toolkits themselves are still published, so the cut is at the right place.</summary>
    [Theory]
    [InlineData("Assets.material")]
    [InlineData("Assets.backdrop")]
    [InlineData("Assets.classify")]
    public void TestTheRealSurfaceSurvivesTheCut(string name) =>
        Assert.Contains(JsSymbolManifest.Symbols, s => s.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    #endregion
}
