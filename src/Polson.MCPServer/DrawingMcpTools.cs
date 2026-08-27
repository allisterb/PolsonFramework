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
    public DrawingMcpTools(JsDrawingEngine? engine = null, SessionRegistry? registry = null, IKnowledgeIndex? knowledge = null)
    {
        Engine = engine ?? new JsDrawingEngine();
        Registry = registry ?? new SessionRegistry();
        Knowledge = knowledge ?? new LocalKnowledgeIndex();
    }
    #endregion

    #region Properties
    public JsDrawingEngine Engine { get; }

    public SessionRegistry Registry { get; }

    public IKnowledgeIndex Knowledge { get; }
    #endregion

    #region Methods
    [McpServerTool(Name = "Search")]
    [Description("Searches the studio's design knowledge and API reference for passages relevant to a technique, " +
        "and returns them ranked with the SDK calls that implement them. The corpus is the studio manuals — classical " +
        "drawing, perspective, lighting, anatomy, composition, logo geometry, typography, distilled from the studio " +
        "reference library — plus the Polson JS SDK core and schema documents. CALL THIS FIRST when you know what you " +
        "want to draw but not how the studio does it (e.g. 'two point perspective box', 'cast shadow falloff', " +
        "'golden ratio logo grid', 'optical kerning'). Each result carries a resource URI to read in full.")]
    public async Task<JsonObject> Search(
        [Description("What you are trying to do or find, in natural language or as an API name (e.g. 'construct a perspective cylinder').")] string query,
        [Description("Number of passages to return (1-25; default 5).")] int? k = null,
        [Description("Corpus to search: 'all' (default), 'manual' for design theory only, 'sdk' for the API reference only.")] string? scope = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var searchScope = scope?.ToLowerInvariant() switch
        {
            "manual" or "manuals" or "theory" or "design" => KnowledgeScope.Manual,
            "sdk" or "api" or "reference" => KnowledgeScope.Sdk,
            _ => KnowledgeScope.All
        };

        var hits = await Knowledge.SearchAsync(query, k ?? 5, searchScope, cancellationToken);

        var results = new JsonArray();
        foreach (var hit in hits)
        {
            results.Add(new JsonObject
            {
                ["uri"] = hit.Uri,
                ["title"] = hit.Title,
                ["section"] = hit.Section,
                ["source"] = hit.Source,
                ["score"] = hit.Score,
                ["apis"] = new JsonArray([.. hit.Apis.Select(a => (JsonNode)JsonValue.Create(a)!)]),
                ["text"] = hit.Text
            });
        }

        return new JsonObject
        {
            ["query"] = query,
            ["scope"] = searchScope.ToString().ToLowerInvariant(),
            ["backend"] = Knowledge.Name,
            ["count"] = results.Count,
            ["results"] = results,
            ["hint"] = results.Count == 0
                ? "No passages matched. Try fewer or more general words, or read `polson://manual/index` for the manual catalogue."
                : "Read the `uri` of a result for the full section. Confirm exact call signatures in `polson://sdk/core/{Area}` before writing the script."
        };
    }

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
