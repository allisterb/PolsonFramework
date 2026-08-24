namespace Polson.MCPServer;

using System;
using System.Text.Json.Nodes;
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
    public DrawingExecutionResult ExecuteSvgScript(string script, int? width = null, int? height = null)
    {
        ArgumentNullException.ThrowIfNull(script);
        return Engine.Execute(script, width ?? 800, height ?? 600);
    }

    public DrawingExecutionResult RenderSvg(string svgXml, int? width = null, int? height = null)
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

    public JsonObject MeasureSvgPath(string pathData, float? length = null)
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

