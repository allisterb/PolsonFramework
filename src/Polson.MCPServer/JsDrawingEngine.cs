namespace Polson.MCPServer;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Jint;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

using Polson.ExtendedMind.ImageGeneration;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;

public partial class JsDrawingEngine : Runtime
{    
    #region Properties
    public static int ScriptTimeoutSeconds { get; set; } = 30;

    public static int MaxStatements { get; set; } = 2_000_000;

    /// <summary>
    /// Asset requisition surface, configured once at startup. Null means the global is not registered
    /// at all; prefer a disabled toolkit, so a script asking for material gets a clear "not
    /// configured" refusal instead of a ReferenceError it cannot interpret.
    /// </summary>
    public static AssetRequisitionToolkit? Assets { get; set; }

    /// <summary>
    /// The project directory a script's file paths resolve against. Null for an ad-hoc server.
    /// </summary>
    /// <remarks>
    /// An instance property rather than a static, unlike the two above: the tests run several
    /// projects in one process, and a static root would let one project's containment apply to
    /// another's execution. Set by <see cref="DrawingMcpTools"/>, which owns the root.
    /// </remarks>
    public string? ProjectRoot { get; set; }

    /// <summary>
    /// The run's record, so a script can declare its stage and add notes. Null outside a project.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="DrawingMcpTools"/>, which owns both the engine and the log. The engine
    /// itself writes no files; it hands the log what to record and the log decides whether it can.
    /// </remarks>
    public RunEventLog? Events { get; set; }
    #endregion
    
    #region Methods
    /// <summary>Synchronous entry point, for callers with no async context.</summary>
    public DrawingExecutionResult Execute(string jsScript, int defaultWidth = 800, int defaultHeight = 600, SessionContext? session = null, string format = "webp", int quality = 85, string? executionId = null) =>
        ExecuteAsync(jsScript, defaultWidth, defaultHeight, session, format, quality, default, executionId).GetAwaiter().GetResult();

    /// <summary>
    /// Executes a script, awaiting any promises it creates.
    /// </summary>
    /// <remarks>
    /// A script containing `await` is wrapped in an async IIFE, because Jint does not allow top-level
    /// await in a plain script. That wrapping changes one thing: inside a function body a bare
    /// trailing expression is no longer the completion value, so an awaiting script must `return`
    /// what it wants rendered. Scripts with no `await` are executed unwrapped and keep exactly their
    /// previous semantics, including a bare trailing `paper;`.
    /// </remarks>
    public async Task<DrawingExecutionResult> ExecuteAsync(string jsScript, int defaultWidth = 800, int defaultHeight = 600, SessionContext? session = null, string format = "webp", int quality = 85, CancellationToken ct = default, string? executionId = null)
    {
        ArgumentNullException.ThrowIfNull(jsScript);

        var result = new DrawingExecutionResult
        {
            ImageFormat = SkiaImageEncoder.NormalizeFormatName(format)
        };
        var sw = Stopwatch.StartNew();
        var papers = new List<SnapPaper>();
        var canvases = new List<SkiaCanvas>();
        var exitRequested = false;
        string? exitMessage = null;

        try
        {
            var engine = new Engine(options =>
            {
                options.Host.StringCompilationAllowed = false;

                // Asset requisition is network I/O returning Task<T>. TaskInterop turns those into
                // JS promises so a script can await them, instead of the engine blocking a thread on
                // a multi-second HTTP call. PromiseTimeout bounds how long one may stay pending.
                options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
                options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(ScriptTimeoutSeconds * 4);
                options.TimeoutInterval(TimeSpan.FromSeconds(ScriptTimeoutSeconds));
                options.LimitRecursion(100);
                options.MaxStatements(MaxStatements);
            });

            // Per-session scratch storage
            engine.SetValue("Session", session?.Storage ?? new Dictionary<string, object?>());

            engine.SetValue("Stage", new StageApi(session, Events, executionId));

            // Pure .NET Console object
            var jsConsole = new JsConsole(result.Logs);
            engine.SetValue("console", jsConsole);

            // Pure .NET Mina animation/easing object
            var mina = new Mina();
            engine.SetValue("mina", mina);

            // Pure .NET Skia API namespace
            var skiaApi = new SkiaApi(ProjectRoot);
            engine.SetValue("Skia", skiaApi);
            engine.SetValue("SK", skiaApi);

            // Pure .NET Constructive Drawing Toolkit
            var drawingToolkit = new ConstructiveDrawingToolkit();
            engine.SetValue("Drawing", drawingToolkit);

            // Pure .NET Logo Design Toolkit
            var logoToolkit = new LogoDesignToolkit();
            engine.SetValue("Logo", logoToolkit);

            // Pure .NET Vector Logo Design Toolkit (Snap.svg)
            var vectorLogoToolkit = new VectorLogoToolkit();
            engine.SetValue("VectorLogo", vectorLogoToolkit);

            // Pure .NET Logotype & Typography Toolkit
            var logoTypeToolkit = new LogoTypeToolkit();
            engine.SetValue("LogoType", logoTypeToolkit);

            // Cloud asset requisition. Registered even when disabled so scripts can branch on the
            // returned failure rather than on the global being absent.
            var assets = Assets ?? new AssetRequisitionToolkit(null, new RequisitionCache(), new AssetBudget(0), "agent");
            engine.SetValue("Assets", assets);

            // Global logging & exit helpers
            engine.SetValue("log", new Action<string>(msg =>
            {
                result.Logs.Add("[LOG] " + msg);
                Runtime.Info("[JS LOG] {0}", msg);
            }));

            engine.SetValue("error", new Action<string>(msg =>
            {
                result.Logs.Add("[ERROR] " + msg);
                Runtime.Error("[JS ERROR] {0}", msg);
            }));

            engine.SetValue("exit", new Action<string>(msg =>
            {
                exitMessage = msg;
                exitRequested = true;
                throw new ExitException(msg);
            }));

            engine.SetValue("table", new ClrFunction(engine, "table", (_, args) =>
            {
                var tableStr = RenderTable(args);
                result.Logs.Add(tableStr);
                Runtime.Info(tableStr);
                return JsValue.Undefined;
            }));

            // 2D Canvas factory functions
            Func<JsValue[], SkiaCanvas> canvasFactory = args =>
            {
                var w = args.Length > 0 && !args[0].IsUndefined() ? Convert.ToInt32(args[0].ToObject()) : defaultWidth;
                var h = args.Length > 1 && !args[1].IsUndefined() ? Convert.ToInt32(args[1].ToObject()) : defaultHeight;
                var canvas = new SkiaCanvas(w, h);
                canvases.Add(canvas);
                return canvas;
            };

            var createCanvasFunc = new ClrFunction(engine, "createCanvas", (_, args) =>
            {
                var c = canvasFactory(args);
                return JsValue.FromObject(engine, c);
            });

            engine.SetValue("createCanvas", createCanvasFunc);

            // `Canvas` is documented as a constructor alias, but a ClrFunction has no [[Construct]],
            // so `new Canvas(w, h)` threw "Canvas is not a constructor". Declaring it in JS fixes
            // that without a TypeReference, which would bypass canvas tracking and so break the
            // documented auto-render of the last canvas a script creates: a JS constructor that
            // returns an object yields that object, so this works with or without `new`.
            engine.Execute("function Canvas(width, height) { return createCanvas(width, height); }");

            // Pure .NET Snap function & namespace
            var snapFunc = new ClrFunction(engine, "Snap", (_, args) =>
            {
                var w = args.Length > 0 && !args[0].IsUndefined() ? args[0].ToObject() : null;
                var h = args.Length > 1 && !args[1].IsUndefined() ? args[1].ToObject() : null;
                var paper = Snap.Create(w ?? defaultWidth, h ?? defaultHeight);
                papers.Add(paper);
                return JsValue.FromObject(engine, paper);
            });

            snapFunc.Set("version", Snap.Version);

            snapFunc.Set("matrix", new ClrFunction(engine, "matrix", (_, args) =>
            {
                float ToFloat(JsValue v, float def = 0f) =>
                    v.IsUndefined() || v.IsNull() ? def : Convert.ToSingle(v.ToObject(), CultureInfo.InvariantCulture);

                var a = args.Length > 0 ? ToFloat(args[0], 1f) : 1f;
                var b = args.Length > 1 ? ToFloat(args[1], 0f) : 0f;
                var c = args.Length > 2 ? ToFloat(args[2], 0f) : 0f;
                var d = args.Length > 3 ? ToFloat(args[3], 1f) : 1f;
                var e = args.Length > 4 ? ToFloat(args[4], 0f) : 0f;
                var f = args.Length > 5 ? ToFloat(args[5], 0f) : 0f;
                return JsValue.FromObject(engine, new SnapMatrix(a, b, c, d, e, f));
            }));

            snapFunc.Set("path", JsValue.FromObject(engine, new SnapPathApi()));

            snapFunc.Set("rgb", new ClrFunction(engine, "rgb", (_, args) =>
            {
                var r = args.Length > 0 && !args[0].IsUndefined() ? Convert.ToInt32(args[0].ToObject()) : 0;
                var g = args.Length > 1 && !args[1].IsUndefined() ? Convert.ToInt32(args[1].ToObject()) : 0;
                var b = args.Length > 2 && !args[2].IsUndefined() ? Convert.ToInt32(args[2].ToObject()) : 0;
                var a = args.Length > 3 && !args[3].IsUndefined() && !args[3].IsNull()
                    ? Convert.ToSingle(args[3].ToObject(), CultureInfo.InvariantCulture)
                    : 1f;
                return Snap.Rgb(r, g, b, a);
            }));

            snapFunc.Set("hsl", new ClrFunction(engine, "hsl", (_, args) =>
            {
                var h = args.Length > 0 && !args[0].IsUndefined() ? Convert.ToSingle(args[0].ToObject(), CultureInfo.InvariantCulture) : 0f;
                var s = args.Length > 1 && !args[1].IsUndefined() ? Convert.ToSingle(args[1].ToObject(), CultureInfo.InvariantCulture) : 0f;
                var l = args.Length > 2 && !args[2].IsUndefined() ? Convert.ToSingle(args[2].ToObject(), CultureInfo.InvariantCulture) : 0f;
                var a = args.Length > 3 && !args[3].IsUndefined() && !args[3].IsNull()
                    ? Convert.ToSingle(args[3].ToObject(), CultureInfo.InvariantCulture)
                    : 1f;
                return Snap.Hsl(h, s, l, a);
            }));

            snapFunc.Set("format", new ClrFunction(engine, "format", (_, args) =>
            {
                var template = args.Length > 0 ? args[0].ToString() : string.Empty;
                var rest = args.Skip(1).Select(x => x.ToObject()).ToArray();
                return Snap.Format(template, rest!);
            }));

            snapFunc.Set("parse", new ClrFunction(engine, "parse", (_, args) =>
            {
                var svg = args.Length > 0 ? args[0].ToString() : string.Empty;
                var paper = Snap.Parse(svg);
                papers.Add(paper);
                return JsValue.FromObject(engine, paper);
            }));

            snapFunc.Set("rad", new ClrFunction(engine, "rad", (_, args) =>
            {
                var deg = args.Length > 0 && !args[0].IsUndefined() ? Convert.ToSingle(args[0].ToObject(), CultureInfo.InvariantCulture) : 0f;
                return Snap.Rad(deg);
            }));

            snapFunc.Set("deg", new ClrFunction(engine, "deg", (_, args) =>
            {
                var rad = args.Length > 0 && !args[0].IsUndefined() ? Convert.ToSingle(args[0].ToObject(), CultureInfo.InvariantCulture) : 0f;
                return Snap.Deg(rad);
            }));

            snapFunc.Set("angle", new ClrFunction(engine, "angle", (_, args) =>
            {
                var x1 = args.Length > 0 && !args[0].IsUndefined() ? Convert.ToSingle(args[0].ToObject(), CultureInfo.InvariantCulture) : 0f;
                var y1 = args.Length > 1 && !args[1].IsUndefined() ? Convert.ToSingle(args[1].ToObject(), CultureInfo.InvariantCulture) : 0f;
                var x2 = args.Length > 2 && !args[2].IsUndefined() ? Convert.ToSingle(args[2].ToObject(), CultureInfo.InvariantCulture) : 0f;
                var y2 = args.Length > 3 && !args[3].IsUndefined() ? Convert.ToSingle(args[3].ToObject(), CultureInfo.InvariantCulture) : 0f;
                return Snap.Angle(x1, y1, x2, y2);
            }));

            snapFunc.Set("snapTo", new ClrFunction(engine, "snapTo", (_, args) =>
            {
                var arr = args.Length > 0 && args[0].ToObject() is object[] objArr
                    ? objArr.Select(x => Convert.ToSingle(x, CultureInfo.InvariantCulture)).ToArray()
                    : [];
                var val = args.Length > 1 && !args[1].IsUndefined() ? Convert.ToSingle(args[1].ToObject(), CultureInfo.InvariantCulture) : 0f;
                var tol = args.Length > 2 && !args[2].IsUndefined() ? Convert.ToSingle(args[2].ToObject(), CultureInfo.InvariantCulture) : 10f;
                return Snap.SnapTo(arr, val, tol);
            }));

            engine.SetValue("Snap", snapFunc);

            var imageDataConstructor = new ClrFunction(engine, "ImageData", (_, args) =>
            {
                if (args.Length >= 2 && args[0].ToObject() is object[] or byte[])
                {
                    var data = args[0].ToObject() is byte[] b
                        ? b
                        : ((object[])args[0].ToObject()!).Select(x => Convert.ToByte(x, CultureInfo.InvariantCulture)).ToArray();
                    var w = Convert.ToInt32(args[1].ToObject(), CultureInfo.InvariantCulture);
                    var h = args.Length > 2
                        ? Convert.ToInt32(args[2].ToObject(), CultureInfo.InvariantCulture)
                        : (data.Length / Math.Max(1, w * 4));
                    return JsValue.FromObject(engine, new ImageData(data, w, h));
                }
                else if (args.Length >= 2)
                {
                    var w = Convert.ToInt32(args[0].ToObject(), CultureInfo.InvariantCulture);
                    var h = Convert.ToInt32(args[1].ToObject(), CultureInfo.InvariantCulture);
                    return JsValue.FromObject(engine, new ImageData(w, h));
                }
                return JsValue.FromObject(engine, new ImageData(1, 1));
            });
            engine.SetValue("ImageData", imageDataConstructor);

            // Standalone path objects for ctx.fill/stroke/clip(path). Mirrors the DOM Path2D
            // constructors: empty, copy, or from an SVG "d" string. Both names are registered
            // because the SDK reference types these parameters as CanvasPath while scripts
            // ported from browser canvas code reach for Path2D.
            // A TypeReference, not a ClrFunction: only the former carries [[Construct]], so `new`
            // works and Jint resolves the three CanvasPath constructors by argument type.
            var canvasPathConstructor = TypeReference.CreateTypeReference<CanvasPath>(engine);

            engine.SetValue("CanvasPath", canvasPathConstructor);
            engine.SetValue("Path2D", canvasPathConstructor);

            // Newlines around the script guard against a trailing line comment swallowing the closer.
            // Only scripts that actually await are wrapped. Inside a function body a bare trailing
            // expression is not a return value, so wrapping unconditionally would silently turn
            // `paper;` at the end of a script into undefined — the documented way to hand back a
            // drawing. Sync scripts therefore keep byte-identical semantics; an awaiting script must
            // use an explicit `return`, which is what the reference tells it to do.
            var needsAwait = AwaitPattern().IsMatch(jsScript);
            var source = needsAwait
                ? $"(async () => {{\n{jsScript}\n}})();"
                : jsScript;

            var evalResult = await engine.EvaluateAsync(source, null, ct);
            sw.Stop();

            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = true;

            SnapPaper? finalPaper = null;
            if (evalResult.ToObject() is SkiaCanvas c)
            {
                result.ReturnValue = c;
                result.ImageBytes = c.ToImageBytes(format, quality);
            }
            else if (evalResult.ToObject() is CanvasRenderingContext2D ctx)
            {
                result.ReturnValue = ctx;
                result.ImageBytes = ctx.Canvas.ToImageBytes(format, quality);
            }
            else if (evalResult.ToObject() is SkiaBitmapWrapper bw)
            {
                result.ReturnValue = bw;
                result.ImageBytes = bw.ToImageBytes(format, quality);
            }
            else if (evalResult.ToObject() is ImageData imgData)
            {
                result.ReturnValue = imgData;
                result.ImageBytes = imgData.ToImageBytes(format, quality);
            }
            else if (evalResult.ToObject() is SnapPaper p)
            {
                finalPaper = p;
                result.ReturnValue = p;
            }
            else if (evalResult.ToObject() is SnapElement el && el.Paper != null)
            {
                finalPaper = el.Paper;
                result.ReturnValue = el;
            }
            else if (canvases.Count > 0)
            {
                var lastCanvas = canvases.Last();
                result.ReturnValue = evalResult.ToObject();
                result.ImageBytes = lastCanvas.ToImageBytes(format, quality);
            }
            else if (papers.Count > 0)
            {
                finalPaper = papers.Last();
                result.ReturnValue = evalResult.ToObject();
            }
            else
            {
                result.ReturnValue = evalResult.ToObject();
            }

            if (finalPaper != null)
            {
                result.SvgXml = finalPaper.ToString();
                result.ImageBytes = finalPaper.ToImageBytes(defaultWidth, defaultHeight, format, quality);
            }
            else if (papers.Count > 0)
            {
                // A raster script keeps the last vector image it produced, as the reference promises.
                // Without this a script that builds a mark in SVG and returns a raster presentation
                // gets an empty SvgXml, and `outSvg` then writes no file and reports no error.
                result.SvgXml = papers.Last().ToString();
            }
        }
        catch (Exception ex) when (exitRequested || ex is ExitException || ex.InnerException is ExitException)
        {
            sw.Stop();
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = true;
            var exitText = exitMessage ?? ex.Message;
            result.Logs.Add("[EXIT] " + exitText);
            Runtime.Info("[JS EXIT] {0}", exitText);
            result.ReturnValue = exitText;

            if (canvases.Count > 0)
            {
                result.ImageBytes = canvases.Last().ToImageBytes(format, quality);
                if (papers.Count > 0)
                {
                    result.SvgXml = papers.Last().ToString();
                }
            }
            else if (papers.Count > 0)
            {
                var finalPaper = papers.Last();
                result.SvgXml = finalPaper.ToString();
                result.ImageBytes = finalPaper.ToImageBytes(defaultWidth, defaultHeight, format, quality);
            }
        }
        catch (PromiseRejectedException prex)
        {
            // An awaited call that threw surfaces here rather than as JavaScriptException, and would
            // otherwise fall through to the generic handler and lose the script-level error text.
            sw.Stop();
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = false;
            result.Error = $"JavaScript error: {prex.Message}";
            Runtime.Error("JavaScript promise rejected: {0}", result.Error);
        }
        catch (JavaScriptException jsex)
        {
            sw.Stop();
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = false;
            result.Error = $"JavaScript error: {jsex.Message}";
            Runtime.Error("JavaScript execution error: {0}", result.Error);
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = false;
            result.Error = ex.Message;
            Runtime.Error(ex, "Script execution error: {0}", ex.Message);
        }

        result.ImageSize = result.ImageBytes?.Length ?? 0;
        return result;
    }

    #region ASCII Table Rendering
    internal static string RenderTable(JsValue[] args)
    {
        if (args.Length == 0 || args[0].IsNull() || args[0].IsUndefined()) return "(empty table)";

        var first = args[0].ToObject();
        var headerNames = args.Length >= 2 && Rows(first) is { } maybe && maybe.All(v => Record(v) is null && Cells(v) is null)
            ? maybe.Select(CellText).ToArray()
            : null;
        var rowsValue = headerNames is not null ? args[1].ToObject() : first;

        if (Rows(rowsValue) is not { } rows)
            return $"(table: expected an array of rows, got {Describe(rowsValue)})";

        var rowList = rows.ToList();
        if (rowList.Count == 0) return "(empty table)";

        var columns = headerNames?.ToList() ?? [];
        if (headerNames is null)
        {
            foreach (var row in rowList)
                foreach (var name in Record(row)?.Keys ?? [])
                    if (!columns.Contains(name, StringComparer.Ordinal)) columns.Add(name);
            if (columns.Count == 0) columns.Add("value");
        }

        var grid = rowList.Select(row => Record(row) is { } record
                ? columns.Select(c => CellText(Lookup(record, c))).ToArray<object?>()
                : Cells(row) is { } cells ? cells.Select(CellText).ToArray<object?>()
                : [CellText(row)])
            .ToArray();
        return RenderAsciiTable(columns.ToArray(), grid);

        static IEnumerable<object?>? Rows(object? v) => v switch
        {
            null or string => null,
            IDictionary<string, object?> => null,
            IEnumerable e => e.Cast<object?>(),
            _ => null,
        };

        static IEnumerable<object?>? Cells(object? v) =>
            v is not string and not IDictionary<string, object?> and IEnumerable e ? e.Cast<object?>() : null;

        static IReadOnlyDictionary<string, object?>? Record(object? v)
        {
            if (v is IDictionary<string, object?> dict) return dict.AsReadOnly();
            if (v is null or string or IEnumerable || v.GetType().IsPrimitive
                || v is decimal or DateTime or DateTimeOffset or TimeSpan or Guid or Enum) return null;
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var p in v.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                try { map[p.Name] = p.GetValue(v); } catch { map[p.Name] = string.Empty; }
            }
            return map.Count > 0 ? map : null;
        }

        static object? Lookup(IReadOnlyDictionary<string, object?> record, string column) =>
            record.TryGetValue(column, out var v) ? v
            : record.FirstOrDefault(kv => string.Equals(kv.Key, column, StringComparison.OrdinalIgnoreCase)).Value;

        static string CellText(object? v) => v switch
        {
            null => string.Empty,
            bool b => b ? "true" : "false",
            string s => s,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable e => string.Join(", ", e.Cast<object?>().Take(5).Select(CellText)),
            _ => v.ToString() ?? string.Empty,
        };

        static string Describe(object? v) => v is null ? "null" : v is string s ? $"the string \"{s}\"" : v.GetType().Name;
    }

    internal static string RenderAsciiTable(string[]? headers, object?[][]? rows)
    {
        headers ??= [];
        rows ??= [];

        var cols = headers.Length;
        foreach (var r in rows) cols = Math.Max(cols, r?.Length ?? 0);
        if (cols == 0) return "(empty table)";

        string[] Header() => Array.ConvertAll(Pad(headers, cols), Cell);
        var grid = new List<string[]> { Header() };
        grid.AddRange(rows.Select(r => Array.ConvertAll(Pad(r ?? [], cols), Cell)));

        var widths = new int[cols];
        foreach (var row in grid)
            for (var c = 0; c < cols; c++)
                widths[c] = Math.Max(widths[c], row[c].Length);

        var separator = "+" + string.Join("+", widths.Select(w => new string('-', w + 2))) + "+";
        string Line(string[] row) =>
            "| " + string.Join(" | ", row.Select((c, i) => c.PadRight(widths[i]))) + " |";

        var sb = new StringBuilder();
        sb.AppendLine(separator);
        sb.AppendLine(Line(grid[0]));
        sb.AppendLine(separator);
        for (var i = 1; i < grid.Count; i++) sb.AppendLine(Line(grid[i]));
        sb.Append(separator);
        return sb.ToString();

        static object?[] Pad(object?[] row, int cols)
        {
            if (row.Length == cols) return row;
            var padded = new object?[cols];
            Array.Copy(row, padded, Math.Min(row.Length, cols));
            return padded;
        }

        static string Cell(object? v)
        {
            var s = v switch
            {
                null => string.Empty,
                bool b => b ? "true" : "false",
                string str => str,
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => v.ToString() ?? string.Empty,
            };
            return s.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        }
    }
    #endregion

    /// <summary>Whole-word `await`, ignoring occurrences inside identifiers.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"\bawait\s")]
    private static partial System.Text.RegularExpressions.Regex AwaitPattern();
    #endregion
}

