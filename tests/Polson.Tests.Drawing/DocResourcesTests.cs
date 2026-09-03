namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Polson.Drawing.Skia;
using Polson.MCPServer;
using Xunit;

public class DocResourcesTests : TestsRuntime
{
    #region Embedded Docs Tests
    [Fact]
    public void TestEmbeddedDocsLoad()
    {
        var core = PolsonResources.Docs.Core();
        Assert.NotNull(core);
        Assert.True(core.Length > 0);
        Assert.Contains("ECMAScript 2025", core);

        var schema = PolsonResources.Docs.Schema();
        Assert.NotNull(schema);
        Assert.True(schema.Length > 0);
        Assert.Contains("DrawingExecutionResult", schema);
    }

    [Fact]
    public void TestNoImplementationMentionsInDocs()
    {
        var core = PolsonResources.Docs.Core();
        Assert.DoesNotContain(".NET", core, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SkiaSharp", core, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Svg.Skia", core, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestDynamicScriptTimeoutInDocs()
    {
        JsDrawingEngine.ScriptTimeoutSeconds = 30;
        var core30 = PolsonResources.Docs.Core();
        Assert.Contains("30 seconds", core30);

        JsDrawingEngine.ScriptTimeoutSeconds = 45;
        var core45 = PolsonResources.Docs.Core();
        Assert.Contains("45 seconds", core45);

        // Reset to default
        JsDrawingEngine.ScriptTimeoutSeconds = 30;
    }
    #endregion

    #region Index & Map Generation Tests
    [Fact]
    public void TestSdkIndexGeneration()
    {
        var index = PolsonResources.SdkIndex();
        Assert.NotNull(index);
        Assert.Contains("Polson JavaScript SDK — Reference Map", index);
        Assert.Contains("polson://sdk/core/", index);
        Assert.Contains("polson://sdk/schema/", index);
        Assert.Contains("## Snap", index);
        Assert.Contains("## Canvas2D", index);
        Assert.Contains("## Skia", index);
        Assert.Contains("## Globals", index);
    }

    [Fact]
    public void TestSdkSchemaSignpost()
    {
        var signpost = PolsonResources.SdkSchema();
        Assert.NotNull(signpost);
        Assert.Contains("polson://sdk/schema/{Area}", signpost);
        Assert.Contains("Snap", signpost);
        Assert.Contains("Canvas2D", signpost);
        Assert.Contains("Skia", signpost);
    }

    /// <summary>
    /// The recession limit is a number an agent reads out of the docs and computes against, so the doc
    /// and the toolkit have to agree on it. The percentage is parsed from the reference rather than
    /// written here twice, and then tested against the call itself — change the constant without the
    /// doc and this fails.
    /// </summary>
    [Fact]
    public void TestDocumentedBoxRecessionLimitMatchesTheToolkit()
    {
        var core = PolsonResources.Docs.Core();
        var stated = Regex.Match(core, @"longer than (\d+)% of the distance from the anchor to its vanishing point");
        Assert.True(stated.Success, "docs/Polson.core.md no longer states the createPerspectiveBox recession limit.");

        var limit = int.Parse(stated.Groups[1].Value, CultureInfo.InvariantCulture) / 100f;
        Assert.Contains($"reach * {limit.ToString("0.##", CultureInfo.InvariantCulture)}", core);

        var toolkit = new ConstructiveDrawingToolkit();
        var grid = toolkit.CreatePerspectiveGrid(new Dictionary<string, object?>
        {
            ["horizonY"] = 250f,
            ["centerOfVisionX"] = 400f,
            ["focalLength"] = 800f,
            ["cameraAngleDeg"] = 40f
        });

        var vpL = (Dictionary<string, object?>)grid["vpL"]!;
        var reach = MathF.Sqrt(
            MathF.Pow(Convert.ToSingle(vpL["x"]) - 400f, 2) + MathF.Pow(Convert.ToSingle(vpL["y"]) - 450f, 2));

        // The documented limit is the accepted one, and a hair past it is not.
        Assert.NotNull(toolkit.CreatePerspectiveBox(grid, 400f, 450f, reach * limit, 120f, 150f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => toolkit.CreatePerspectiveBox(grid, 400f, 450f, reach * (limit + 0.02f), 120f, 150f));
    }
    #endregion

    #region Slicing Tests
    [Fact]
    public void TestPerAreaCoreSlicing()
    {
        var core = PolsonResources.Docs.Core();

        var snapSlice = SdkDocs.Slice(core, "Snap");
        Assert.NotNull(snapSlice);
        Assert.Contains("Snap(", snapSlice);
        Assert.Contains("SnapPaper", snapSlice);
        Assert.Contains("SnapElement", snapSlice);

        var canvasSlice = SdkDocs.Slice(core, "Canvas2D");
        Assert.NotNull(canvasSlice);
        Assert.Contains("createCanvas", canvasSlice);
        Assert.Contains("CanvasRenderingContext2D", canvasSlice);
        Assert.Contains("drawImage", canvasSlice);

        var skiaSlice = SdkDocs.Slice(core, "Skia");
        Assert.NotNull(skiaSlice);
        Assert.Contains("Skia.Shader", skiaSlice);
        Assert.Contains("Skia.ImageFilter", skiaSlice);
        Assert.Contains("SkiaBitmapWrapper", skiaSlice);

        var globalsSlice = SdkDocs.Slice(core, "Global Functions");
        Assert.NotNull(globalsSlice);
        Assert.Contains("console", globalsSlice);
        Assert.Contains("exit(message: string)", globalsSlice);
    }

    [Fact]
    public void TestPerAreaSchemaSlicing()
    {
        var schema = PolsonResources.Docs.Schema();

        var snapSlice = SdkDocs.Slice(schema, "Snap");
        Assert.NotNull(snapSlice);
        Assert.Contains("SnapBBox", snapSlice);

        var canvasSlice = SdkDocs.Slice(schema, "Canvas2D");
        Assert.NotNull(canvasSlice);
        Assert.Contains("TextMetrics", canvasSlice);

        var skiaSlice = SdkDocs.Slice(schema, "Skia");
        Assert.NotNull(skiaSlice);
        Assert.Contains("ImageData", skiaSlice);
        Assert.Contains("SkiaBitmapWrapper", skiaSlice);
    }
    #endregion

    #region Area Resources Enumeration Tests
    [Fact]
    public void TestAreaResourcesRegistration()
    {
        var resources = PolsonResources.AreaResources(PolsonResources.Docs).ToList();
        Assert.NotEmpty(resources);

        var uris = resources.Select(r => r.ProtocolResource?.Uri).Where(u => u != null).ToList();
        Assert.Contains("polson://sdk/core/Snap", uris);
        Assert.Contains("polson://sdk/schema/Snap", uris);
        Assert.Contains("polson://sdk/core/Canvas2D", uris);
        Assert.Contains("polson://sdk/schema/Canvas2D", uris);
        Assert.Contains("polson://sdk/core/Skia", uris);
        Assert.Contains("polson://sdk/schema/Skia", uris);
        Assert.Contains("polson://sdk/core/Globals", uris);

        foreach (var resource in resources)
        {
            Assert.NotNull(resource.ProtocolResource);
            Assert.Equal("text/plain", resource.ProtocolResource!.MimeType);
            Assert.NotNull(resource.ProtocolResource.Title);
            Assert.NotNull(resource.ProtocolResource.Description);
        }
    }
    #endregion
}

