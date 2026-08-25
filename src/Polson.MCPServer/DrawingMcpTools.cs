namespace Polson.MCPServer;

using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Polson.Drawing.Svg;

public class DrawingMcpTools
{
    #region Constructors
    public DrawingMcpTools(JsDrawingEngine? engine = null)
    {
        Engine = engine ?? new JsDrawingEngine();
    }
    #endregion

    #region Properties
    public JsDrawingEngine Engine { get; }
    #endregion

    #region Methods
    [McpServerTool(Name = "ExecuteScript")]
    [Description("Executes a JavaScript drawing script inside the sandboxed graphics engine, supporting Snap.svg vector graphics, HTML5 2D Canvas, and Skia procedural shaders, filters, and image processing. Automatically renders returned paper/canvas/bitmap/image-data to PNG bytes and SVG markup.")]
    public DrawingExecutionResult ExecuteScript(
        [Description("The JavaScript code to execute.")] string script,
        [Description("Default canvas / SVG viewport width in pixels (default 800).")] int? width = null,
        [Description("Default canvas / SVG viewport height in pixels (default 600).")] int? height = null)
    {
        ArgumentNullException.ThrowIfNull(script);
        return Engine.Execute(script, width ?? 800, height ?? 600);
    }

    public DrawingExecutionResult ExecuteSvgScript(string script, int? width = null, int? height = null)
        => ExecuteScript(script, width, height);

    [McpServerTool(Name = "RenderSvg")]
    [Description("Headlessly renders raw SVG XML markup to a PNG byte array.")]
    public DrawingExecutionResult RenderSvg(
        [Description("The SVG XML string to render.")] string svgXml,
        [Description("Target image width in pixels (optional, defaults to SVG width or 800).")] int? width = null,
        [Description("Target image height in pixels (optional, defaults to SVG height or 600).")] int? height = null)
    {
        ArgumentNullException.ThrowIfNull(svgXml);

        var result = new DrawingExecutionResult
        {
            SvgXml = svgXml
        };

        try
        {
            var png = SvgRenderPipeline.RenderToPng(svgXml, width, height);
            result.Success = true;
            result.PngBytes = png;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    [McpServerTool(Name = "MeasureSvgPath")]
    [Description("Measures an SVG path definition to calculate its total length, bounding box, and optional point coordinates at length.")]
    public JsonObject MeasureSvgPath(
        [Description("The SVG path data string (e.g. 'M10 10 L50 50 Z').")] string pathData,
        [Description("Optional distance along the path to sample coordinates and tangent angle.")] float? length = null)
    {
        ArgumentNullException.ThrowIfNull(pathData);

        var totalLength = SnapPathMeasurement.GetTotalLength(pathData);
        var bbox = SnapPathMeasurement.GetBBox(pathData);

        var response = new JsonObject
        {
            ["totalLength"] = totalLength,
            ["bbox"] = new JsonObject
            {
                ["x"] = bbox.X,
                ["y"] = bbox.Y,
                ["width"] = bbox.Width,
                ["height"] = bbox.Height,
                ["cx"] = bbox.Cx,
                ["cy"] = bbox.Cy
            }
        };

        if (length.HasValue)
        {
            var pt = SnapPathMeasurement.GetPointAtLength(pathData, length.Value);
            response["pointAtLength"] = new JsonObject
            {
                ["x"] = pt.X,
                ["y"] = pt.Y,
                ["alpha"] = pt.Alpha
            };
        }

        return response;
    }
    #endregion
}

