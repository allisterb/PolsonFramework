namespace Polson.Tests.Drawing;

using System;
using System.Linq;
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

