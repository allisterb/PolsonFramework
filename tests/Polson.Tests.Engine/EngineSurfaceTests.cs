namespace Polson.Tests.Engine;

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// What Polson.Engine leaves out, checked against the build: the drawing engine with none of the studio. The engine
/// shares its source with the studio's server, so these are the tests that fail if a studio part leaks back into a
/// shared file.
/// </summary>
public class EngineSurfaceTests : TestsRuntime
{
    #region Tests
    [Theory]
    [InlineData("Assets")]
    [InlineData("Photo")]
    [InlineData("Documents")]
    [InlineData("Video")]
    [InlineData("Verify")]
    [InlineData("Research")]
    [InlineData("Taste")]
    public void TestTheStudioGlobalIsAbsent(string name) =>
        Assert.Equal("undefined", TypeOf(name));

    [Theory]
    [InlineData("Snap")]
    [InlineData("createCanvas")]
    [InlineData("Skia")]
    [InlineData("Drawing")]
    [InlineData("Logo")]
    [InlineData("Motion")]
    [InlineData("Mesh")]
    [InlineData("Face")]
    [InlineData("Character")]
    [InlineData("Stage")]
    [InlineData("Session")]
    [InlineData("ARGS")]
    public void TestTheEngineGlobalIsPresent(string name) =>
        Assert.NotEqual("undefined", TypeOf(name));

    /// <summary>The run record's half of <c>Stage</c> is the engine's: forty examples in the reference call it.</summary>
    [Fact]
    public void TestStageChecksWithoutTheStudio()
    {
        var result = Run("Stage.check('the circle is red', true, 'measured #e63946'); Stage.note('a note'); Stage.expect('a claim');");

        Assert.True(result.Success, result.Error);
        Assert.Contains("[CHECK] PASS the circle is red - measured #e63946", result.Logs);
    }

    [Theory]
    [InlineData("book")]
    [InlineData("mockup")]
    [InlineData("mindMap")]
    [InlineData("matrix")]
    [InlineData("shape")]
    [InlineData("borders")]
    public void TestTheStudioSheetIsAbsent(string member)
    {
        var result = Run($"log(String(has(Stage, '{member}')));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("[LOG] false", result.Logs);
    }

    [Fact]
    public void TestTheAssemblyIsTheEngineAndReferencesNoCloud()
    {
        var assembly = typeof(JsDrawingEngine).Assembly;

        Assert.Equal("Polson.Engine", assembly.GetName().Name);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name is "Polson.ExtendedMind" or "Polson.MCPServer");
    }

    [Fact]
    public void TestTheToolsAreTheEngines()
    {
        var tools = typeof(DrawingMcpTools).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .Order(StringComparer.Ordinal);

        Assert.Equal(["CompareImages", "ExecuteScript", "History", "InspectScript", "MeasureSvgPath", "ReadDoc", "Recall",
            "RenderSvg", "ScanText", "Search"], tools);
    }

    [Fact]
    public void TestTheReferenceIsTheCoreAlone()
    {
        Assert.DoesNotContain("Studio Reference", PolsonResources.Docs.Core());
        Assert.Null(PolsonResources.Read("polson://sdk/core/Assets"));
    }

    [Theory]
    [InlineData("Assets")]
    [InlineData("Photo")]
    [InlineData("Documents")]
    [InlineData("Video")]
    [InlineData("Research")]
    public void TestTheIndexLeavesOutAnAreaTheEngineDoesNotHave(string area)
    {
        Assert.DoesNotContain($"## {area}\n", PolsonResources.SdkIndex().Replace("\r", ""));
        Assert.DoesNotContain(area, PolsonResources.SdkSchema().Split("Areas:")[1]);
    }

    [Fact]
    public void TestEveryKnownUriResolvesAndNoneIsAManual()
    {
        var known = PolsonResources.KnownUris();

        Assert.Empty(known.Where(u => string.IsNullOrWhiteSpace(PolsonResources.Read(u))));
        Assert.DoesNotContain(known, u => u.StartsWith("polson://manual", StringComparison.Ordinal));
        Assert.Contains("polson://sdk/core/Motion", known);
    }

    [Theory]
    [InlineData("polson://manual/index")]
    [InlineData("polson://manual/13")]
    [InlineData("13")]
    public void TestNoManualIsServed(string uri) =>
        Assert.Null(PolsonResources.Read(uri));

    [Fact]
    public async Task TestSearchFindsTheReference()
    {
        var hits = await new LocalKnowledgeIndex().SearchAsync("two point perspective box", 5, KnowledgeScope.All);

        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.StartsWith("polson://sdk/", h.Uri));
    }
    #endregion

    #region Methods
    private static DrawingExecutionResult Run(string script) => new JsDrawingEngine().Execute(script, 100, 100, render: false);

    private static string TypeOf(string name)
    {
        var result = Run($"log(typeof globalThis['{name}']);");
        Assert.True(result.Success, result.Error);
        return result.Logs.Single(l => l.StartsWith("[LOG] ", StringComparison.Ordinal))["[LOG] ".Length..];
    }
    #endregion
}
