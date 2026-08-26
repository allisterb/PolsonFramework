namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;

public class DrawingMcpTools
{
    #region Constants & Static Properties
    public static TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(1);
    #endregion

    #region Constructors
    public DrawingMcpTools(JsDrawingEngine? engine = null, SessionRegistry? registry = null)
    {
        Engine = engine ?? new JsDrawingEngine();
        Registry = registry ?? new SessionRegistry();
    }
    #endregion

    #region Properties
    public JsDrawingEngine Engine { get; }

    public SessionRegistry Registry { get; }
    #endregion

    #region Methods
    [McpServerTool(Name = "ExecuteScript")]
    [Description("Executes a JavaScript drawing script inside the sandboxed graphics engine, supporting Snap.svg vector graphics, HTML5 2D Canvas, and Skia procedural shaders, filters, and image processing. Automatically renders returned paper/canvas/bitmap/image-data to WebP/PNG/JPEG bytes and SVG markup.")]
    public async Task<DrawingExecutionResult> ExecuteScript(
        [Description("The JavaScript code to execute.")] string script,
        [Description("Default canvas / SVG viewport width in pixels (default 800).")] int? width = null,
        [Description("Default canvas / SVG viewport height in pixels (default 600).")] int? height = null,
        [Description("Output image encoding format ('webp', 'png', 'jpeg'; default 'webp').")] string? format = null,
        [Description("Image encoding quality (1-100; default 85).")] int? quality = null,
        [Description("Optional file path where the rendered image should be saved directly (e.g. 'artifacts/stage1.webp').")] string? outFile = null,
        [Description("Optional file path where the rendered SVG XML should be saved directly (e.g. 'artifacts/stage1.svg').")] string? outSvg = null,
        [Description("Whether to include base64 imageBytes in the JSON response (default: true if outFile is omitted, false if outFile is specified).")] bool? includeBytes = null,
        RequestContext<CallToolRequestParams>? context = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        var sessionId = GetSessionId(context?.Server);
        var session = Registry.GetOrCreate(sessionId);

        lock (session.ScriptHistory)
        {
            session.ScriptHistory.Add(script);
        }

        session.EnterCall();
        try
        {
            var fmt = format ?? "webp";
            var q = quality ?? 85;
            var runTask = Task.Run(() => Engine.Execute(script, width ?? 800, height ?? 600, session, fmt, q), cancellationToken);
            var result = await RunWithHeartbeatAsync(runTask, progress, HeartbeatInterval, cancellationToken);

            if (!string.IsNullOrWhiteSpace(outFile) && result.ImageBytes != null && result.ImageBytes.Length > 0)
            {
                var fullOutPath = Path.GetFullPath(outFile);
                var dir = Path.GetDirectoryName(fullOutPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllBytes(fullOutPath, result.ImageBytes);
                result.ImageFilePath = fullOutPath;
            }

            if (!string.IsNullOrWhiteSpace(outSvg) && !string.IsNullOrWhiteSpace(result.SvgXml))
            {
                var fullSvgPath = Path.GetFullPath(outSvg);
                var dir = Path.GetDirectoryName(fullSvgPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(fullSvgPath, result.SvgXml);
                result.SvgFilePath = fullSvgPath;
            }

            result.ImageSize = result.ImageBytes?.Length ?? 0;
            var shouldIncludeBytes = includeBytes ?? string.IsNullOrWhiteSpace(outFile);
            if (!shouldIncludeBytes)
            {
                result.ImageBytes = null;
            }

            return result;
        }
        finally
        {
            session.LeaveCall();
        }
    }

    public Task<DrawingExecutionResult> ExecuteSvgScript(
        string script,
        int? width = null,
        int? height = null,
        string? format = null,
        int? quality = null,
        string? outFile = null,
        string? outSvg = null,
        bool? includeBytes = null)
        => ExecuteScript(script, width, height, format, quality, outFile, outSvg, includeBytes);

    [McpServerTool(Name = "History")]
    [Description("Returns the last n scripts executed by the agent in this session. If n is null or omitted, returns the last script.")]
    public List<string> History(
        [Description("The number of recent scripts to return. If null or omitted, returns the last script.")] int? n = null,
        RequestContext<CallToolRequestParams>? context = null)
    {
        var sessionId = GetSessionId(context?.Server);
        var session = Registry.GetOrCreate(sessionId);

        lock (session.ScriptHistory)
        {
            var count = n ?? 1;
            if (count <= 0) return [];
            return session.ScriptHistory.TakeLast(count).ToList();
        }
    }

    [McpServerTool(Name = "RenderSvg")]
    [Description("Headlessly renders raw SVG XML markup to a WebP/PNG/JPEG byte array.")]
    public DrawingExecutionResult RenderSvg(
        [Description("The SVG XML string to render.")] string svgXml,
        [Description("Target image width in pixels (optional, defaults to SVG width or 800).")] int? width = null,
        [Description("Target image height in pixels (optional, defaults to SVG height or 600).")] int? height = null,
        [Description("Output image encoding format ('webp', 'png', 'jpeg'; default 'webp').")] string? format = null,
        [Description("Image encoding quality (1-100; default 85).")] int? quality = null,
        [Description("Optional file path where the rendered image should be saved directly.")] string? outFile = null,
        [Description("Whether to include base64 imageBytes in the JSON response (default: true if outFile is omitted, false if outFile is specified).")] bool? includeBytes = null)
    {
        ArgumentNullException.ThrowIfNull(svgXml);

        var fmt = format ?? "webp";
        var q = quality ?? 85;
        var result = new DrawingExecutionResult
        {
            SvgXml = svgXml,
            ImageFormat = SkiaImageEncoder.NormalizeFormatName(fmt)
        };

        try
        {
            var imgBytes = SvgRenderPipeline.RenderToImage(svgXml, width, height, fmt, q);
            result.Success = true;
            result.ImageBytes = imgBytes;

            if (!string.IsNullOrWhiteSpace(outFile) && imgBytes != null && imgBytes.Length > 0)
            {
                var fullOutPath = Path.GetFullPath(outFile);
                var dir = Path.GetDirectoryName(fullOutPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllBytes(fullOutPath, imgBytes);
                result.ImageFilePath = fullOutPath;
            }

            result.ImageSize = result.ImageBytes?.Length ?? 0;
            var shouldIncludeBytes = includeBytes ?? string.IsNullOrWhiteSpace(outFile);
            if (!shouldIncludeBytes)
            {
                result.ImageBytes = null;
            }
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

    internal static async Task<T> RunWithHeartbeatAsync<T>(
        Task<T> task,
        IProgress<ProgressNotificationValue>? progress,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        if (progress == null)
        {
            return await task;
        }

        using var timer = new PeriodicTimer(interval);
        float tick = 0f;
        while (!task.IsCompleted)
        {
            var waitTick = timer.WaitForNextTickAsync(cancellationToken).AsTask();
            var completed = await Task.WhenAny(task, waitTick);
            if (completed == task)
            {
                return await task;
            }
            tick++;
            progress.Report(new ProgressNotificationValue { Progress = tick, Total = null });
        }
        return await task;
    }

    private static string GetSessionId(McpServer? server)
    {
        if (server == null) return "default";
        return server.SessionId ?? "default";
    }
    #endregion
}
