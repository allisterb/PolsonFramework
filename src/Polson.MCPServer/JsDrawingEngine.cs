namespace Polson.MCPServer;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

using Polson.ExtendedMind.DocumentProcessing;
using Polson.ExtendedMind.ImageGeneration;
using Polson.ExtendedMind.ParallelSearch;
using Polson.ExtendedMind.Photos;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;

public partial class JsDrawingEngine : Runtime
{
    #region Constants
    /// <summary>
    /// Members the JS runtime probes on arbitrary values as part of a language protocol rather than
    /// because a script asked for them. Exempt from <c>Interop.ThrowOnUnresolvedMember</c>.
    /// </summary>
    /// <remarks>
    /// <c>then</c> is the thenable check every <c>await</c> performs on the value it resolves;
    /// <c>toJSON</c> is <c>JSON.stringify</c> looking for a custom serializer. Both are absent on our
    /// types by design, and undefined is the correct answer. Add to this list only for a name the
    /// <i>engine</i> reads unprompted — anything a script would plausibly type belongs in the API or
    /// in an error message.
    /// </remarks>
    /// <summary>The canvases and papers of the execution now running.</summary>
    /// <remarks>
    /// Ambient rather than captured, because a function kept in <c>Session</c> carries the
    /// <c>createCanvas</c> of the script that defined it. Captured lists filed its canvas under that
    /// earlier script, so a later script calling the helper rendered nothing and still reported success,
    /// and <c>outFile</c> wrote no file (lastlight2, 2026-09-25).
    /// </remarks>
    private static readonly AsyncLocal<(List<SkiaCanvas> Canvases, List<SnapPaper> Papers)?> CurrentDrawing = new();

    private static readonly HashSet<string> InteropProtocolMembers = new(StringComparer.Ordinal)
    {
        "then",
        "toJSON",
    };
    #endregion

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
    /// Document reading, configured once at startup. Registered on the same terms as
    /// <see cref="Assets"/>, and for the same reason a photograph surface is: a script that cannot
    /// read the document it was pointed at must be *told* so, because the failure it would otherwise
    /// reach for is answering from recall and presenting it as sourced.
    /// </summary>
    public static DocumentProcessor? Documents { get; set; }

    /// <summary>
    /// Reference-photograph surface, configured once at startup. Null means the same thing it means
    /// for <see cref="Assets"/> — a disabled toolkit is registered instead, so a script asking for a
    /// likeness is refused in words rather than by a ReferenceError.
    /// </summary>
    public static PhotoToolkit? Photos { get; set; }

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
    public DrawingExecutionResult Execute(string jsScript, int defaultWidth = 800, int defaultHeight = 600, SessionContext? session = null, string format = "webp", int quality = 85, string? executionId = null, bool render = true) =>
        ExecuteAsync(jsScript, defaultWidth, defaultHeight, session, format, quality, default, executionId, render).GetAwaiter().GetResult();

    /// <summary>
    /// Rasterises a returned paper at <b>its own</b> size, falling back to the default viewport only
    /// when the document states none.
    /// </summary>
    /// <remarks>
    /// <c>defaultWidth</c>/<c>defaultHeight</c> are the size a script gets from a bare
    /// <c>Snap()</c> or <c>createCanvas()</c> — a default <i>viewport</i>, not a render target. They
    /// were being handed to <c>ToImageBytes</c> as well, so a paper built at <c>Snap(900, 1350)</c>
    /// was squeezed into 800x600; and because the pipeline scales each axis separately, the result
    /// was not letterboxed but <b>anamorphic</b> — 0.89x across against 0.44x down.
    /// <para>
    /// That is worse than a mismatched thumbnail, because the raster peek is how an agent sees its
    /// own work: on the kubrick7 run every visual judgment — crowding, balance, whether a label fit —
    /// was made against a picture that was not the deliverable. The canvas branch never had the
    /// fault, since it encodes at the canvas's own size; this makes the paper branch agree with it,
    /// and with the file <c>outSvg</c> writes.
    /// </para>
    /// <para>
    /// The size is read from the document rather than left to the pipeline's <c>null</c> default,
    /// which measures the rendered picture's bounds — a mark drawn outside the viewport would then
    /// change the peek's dimensions while the deliverable's stayed put, which is the same class of
    /// disagreement in a smaller size.
    /// </para>
    /// </remarks>
    private static byte[] RenderPaper(SnapPaper paper, int defaultWidth, int defaultHeight, string format, int quality)
    {
        var width = paper.Width > 0 ? (int)MathF.Ceiling(paper.Width) : defaultWidth;
        var height = paper.Height > 0 ? (int)MathF.Ceiling(paper.Height) : defaultHeight;
        return paper.ToImageBytes(width, height, format, quality);
    }

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
    public async Task<DrawingExecutionResult> ExecuteAsync(string jsScript, int defaultWidth = 800, int defaultHeight = 600, SessionContext? session = null, string format = "webp", int quality = 85, CancellationToken ct = default, string? executionId = null, bool render = true)
    {
        ArgumentNullException.ThrowIfNull(jsScript);

        var result = new DrawingExecutionResult
        {
            ImageFormat = SkiaImageEncoder.NormalizeFormatName(format)
        };
        var sw = Stopwatch.StartNew();

        // Rendering the result to image bytes happens after `sw` has stopped, and on this scene it
        // costs three to four times what evaluating the script does. Timed separately rather than
        // folded in: `ExecutionTimeMs` is published in the run record and in the SDK reference, so
        // widening it would silently change what every recorded number means.
        var encodeSw = new Stopwatch();

        // The single gate for `render: false`. A script that only measures — sampling pixels,
        // diffing against an earlier stage, stashing a canvas in Session for the next call — would
        // otherwise pay a full rasterise and encode for an image nothing ever looks at, because a
        // canvas is rendered whenever one was created and something else was returned.
        byte[]? Encode(Func<byte[]> encode)
        {
            if (!render) return null;
            encodeSw.Start();
            try { return encode(); }
            finally { encodeSw.Stop(); }
        }

        var papers = new List<SnapPaper>();
        var canvases = new List<SkiaCanvas>();
        CurrentDrawing.Value = (canvases, papers);
        MotionToolkit? motionToolkit = null;
        var exitRequested = false;
        string? exitMessage = null;

        // Declared out here so the catch can read it: a rejection is explained after the try has been
        // left, and whether the script was wrapped is what decides the line arithmetic.
        var needsAwait = false;

        // Declared out here because settling happens after the catches: a script that threw or ran
        // out of time still wrote to the scratchpad, and discarding that would make an error lose
        // work the run had already done.
        Dictionary<string, JsValue>? liveSession = null;
        Engine? sessionEngine = null;

        // One pixel view per ImageData, for this engine only: an ImageData kept in Session outlives
        // the engine that first wrapped it, and a view belongs to the realm that made it.
        var pixelViews = new ConditionalWeakTable<ImageData, JsValue>();

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

                // A script names an enum with a string, because that is what the SDK reference
                // documents. See EnumStringTypeConverter.
                options.SetTypeConverter(e => new EnumStringTypeConverter(e));

                // A misspelled member is an error, not a new property. By default Jint lets a script
                // invent members on a wrapped .NET object: `ctx.fillStlye = 'red'` silently created a
                // JS-side property, the fill stayed black, and the script had no way to find out —
                // the same mechanism let `canvas.width = 999` read back as 999 on a canvas still 16
                // wide, and let a brush preset report a colour it would never draw. Every one of
                // those cost a render and a round of guessing about why the picture was unchanged.
                options.Interop.ThrowOnUnresolvedMember = true;

                // ...except the handful of names the JS runtime itself probes on arbitrary values.
                // `await x` reads `x.then` to decide whether x is a thenable, and JSON.stringify
                // reads `toJSON`; with the setting above, those internal probes would throw on every
                // .NET object — which broke `await Assets.material(...)` outright. Returning
                // undefined is what the absence of a thenable/serializer hook is supposed to mean.
                // Keep this list minimal: exempting `toString` here would shadow the real
                // `paper.toString()`, and the point is to hide protocol, not API.
                // ...and except a member that simply is not there, which reads as `undefined` rather
                // than throwing. `typeof ctx.foo`, `'foo' in ctx` and `?.` are how JavaScript asks
                // whether something exists, and an engine that kills the script for asking is one
                // every agent has to learn the hard way — a measured run spent 18 of its 71 renders
                // discovering it, one deleted candidate name at a time.
                //
                // Three things keep this from being the silent-typo bug it looks like:
                //
                //   - Writes are unaffected. Jint consults this accessor on *reads* only, so
                //     `ctx.fillStlye = 'red'` still throws — that is the case that cost real renders.
                //   - Calls are unaffected. `MissingCallResolver` puts the "did you mean" message
                //     back on the call path, where the reference still knows its own name and base.
                //   - Reads are *recorded*. A misspelled read is genuinely silent to the script —
                //     `ctx.lineWidht * 2` is NaN — so the defence is seeing it rather than
                //     preventing it, and every one lands in the run record as an `absent` probe.
                options.SetMemberAccessor((e, target, member) =>
                {
                    // `imageData.data` is a Uint8ClampedArray over the ImageData's own byte[], as in
                    // a browser. Left to the default conversion, every read copied the buffer into a
                    // new JS array, so `img.data === img.data` was false and every write was silently
                    // lost: putImageData then put back the pixels it had been given. The ArrayBuffer
                    // takes the array as its backing store rather than copying it, so a write lands
                    // in the buffer putImageData reads.
                    if (target is ImageData image && member is "data")
                    {
                        return pixelViews.GetValue(image, i =>
                            e.Construct("Uint8ClampedArray", e.Intrinsics.ArrayBuffer.Construct(i.Data)));
                    }

                    // `then` is answered undefined *unconditionally*, because `await` probes it on
                    // every value it resolves — a type that happened to carry a `Then` would
                    // silently hijack awaiting, which is not a trade worth any convenience.
                    if (member is "then") return JsValue.Undefined;

                    // A list is answered here, not by MemberIndex.Has, which says yes to every name on
                    // a list because it cannot tell a declared member from one Array.prototype
                    // attaches. That yes sent every read to reflection, and a name found nowhere threw
                    // the unresolved-member exception - which is not a JS error, so `try/catch` could
                    // not stop it, and JSON.stringify, asking each value for `toJSON`, killed any
                    // script that stringified a value holding a list. Now a list reads like any other
                    // SDK object: its own members and Array.prototype's resolve, anything else is
                    // undefined.
                    if (IsList(target))
                    {
                        // Where a .NET collection method shares a name with Array.prototype - forEach,
                        // indexOf, find, sort, reverse - the JS meaning wins. Left to reflection,
                        // `list.forEach((x, i) => ...)` reached List<T>.ForEach, which passes the item
                        // alone, so every index was undefined: the documented quirk on Assets.library.
                        var prototype = (ObjectInstance)e.Intrinsics.Array.Get("prototype");
                        if (target!.GetType().Namespace?.StartsWith("System", StringComparison.Ordinal) == true
                            && member is not ("length" or "constructor")
                            && prototype.HasProperty(member))
                        {
                            return prototype.Get(member);
                        }

                        if (Resolves(e, target, member)) return null;

                        ProbeScope.RecordOutcome(ProbeScope.Kinds.Absent, $"{target!.GetType().Name}.{member}");
                        return JsValue.Undefined;
                    }

                    // Everything else that really exists answers for itself — **including
                    // `toJSON`**, which is a hook a type is *meant* to be able to implement.
                    // Returning undefined for it unconditionally, as this did, meant a `ToJSON()`
                    // written on a result type could never be reached: `JSON.stringify` went on
                    // walking the CLR object, emitting raw `Bytes` as a decimal array under
                    // PascalCase keys. The exemption exists so an *absent* hook reads as absent
                    // rather than throwing, and that is all it should do.
                    if (MemberIndex.Has(target, member)) return null;
                    if (InteropProtocolMembers.Contains(member)) return JsValue.Undefined;

                    // Only the misses, and only ones with a real receiver — cheap because a script
                    // that spells everything correctly never reaches this line.
                    if (target is not null)
                    {
                        ProbeScope.RecordOutcome(ProbeScope.Kinds.Absent,
                            $"{target.GetType().Name}.{member}");
                    }

                    return JsValue.Undefined;
                });

                options.ReferenceResolver = new MissingCallResolver();

                // A typo'd variable is an error too, rather than a new global.
                options.Strict = true;
            });

            // Ask whether a call exists before committing to it. Resolution is strict, so every
            // ordinary idiom for this — `typeof ctx.foo`, `'foo' in ctx`, `Object.hasOwn`,
            // `Reflect.has` — throws instead of answering. See `MemberIndex` for why that is kept.
            engine.SetValue("has", new Func<object?, string?, bool>(
                (target, member) => member is not null && Resolves(engine, target, member)));

            // ...and why not, when it is not. `has` answers whether a name exists; on its own that
            // leaves a script knowing it guessed wrong and not what to write instead — which is the
            // half of the failed access that was actually useful. The suggester was previously
            // reachable only by throwing, so the cheap way to get advice was to make a mistake.
            engine.SetValue("suggest", new Func<object?, string?, string>((target, member) =>
            {
                if (target is null || string.IsNullOrWhiteSpace(member))
                {
                    return "suggest(object, 'name') needs an object and a member name.";
                }

                if (Resolves(engine, target, member))
                {
                    // Worth saying plainly. A caller reaching for advice about a name that is already
                    // correct is looking in the wrong place for its bug, and silence would let it go
                    // on looking.
                    return $"'{member}' exists on {target.GetType().Name} — nothing to correct.";
                }

                // The message Jint would have produced, so one explainer serves the probe and the
                // throw rather than two that drift apart.
                return ExplainMissingMember(
                    $"Cannot access property '{member}' on type '{target.GetType().FullName}'");
            }));

            // Per-session scratch storage. The engine is handed a map of live `JsValue`, not the
            // durable CLR store, so a value the script mutates after storing it is still the value
            // that gets kept - see SessionBridge for what that fixes and why.
            liveSession = SessionBridge.Open(engine, session?.Storage ?? new Dictionary<string, object?>());
            sessionEngine = engine;
            engine.SetValue("Session", liveSession);

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

            // Chart models were always surface-agnostic arithmetic — 2,436 lines of ChartToolkit of
            // which exactly one method takes a canvas. This is the vector half of the drawing they
            // lacked, so a chart can reach an SVG deliverable. Also reachable as paper.chart(model).
            engine.SetValue("VectorChart", new VectorChartToolkit());

            // Pure .NET Logotype & Typography Toolkit
            var logoTypeToolkit = new LogoTypeToolkit();
            engine.SetValue("LogoType", logoTypeToolkit);

            // Rectangle arithmetic for page composition. Pairs with ctx.measureWrappedText(...),
            // which is where content-driven sizes come from.
            var layoutToolkit = new LayoutToolkit();
            engine.SetValue("Layout", layoutToolkit);

            // Stylesheets read as design languages: tokens and text styling, never layout.
            var cssToolkit = new Polson.HtmlParser.CssToolkit();
            engine.SetValue("Css", cssToolkit);

            // Value-to-pixel mapping: the numeric spine of a chart. Draws nothing itself.
            var scaleToolkit = new ScaleToolkit();
            engine.SetValue("Scale", scaleToolkit);

            // Reproducible randomness. `Math.random` is still there and still works; this is the one
            // a run can be re-rendered from, which is what the saved script in `scripts/` promises.
            var randomToolkit = new RandomToolkit();
            engine.SetValue("Random", randomToolkit);

            // A staged composition as a construction: depth layers, named slots, and the colour
            // system that holds them together. Returns a model and draws nothing, like Chart.
            var sceneToolkit = new SceneToolkit();
            engine.SetValue("Scene", sceneToolkit);

            // Whole charts as constructions, above the Scale/Layout primitives. Returns models and
            // geometry rather than drawing, on the createMannequinFigure/createFigureGeometry split.
            var chartToolkit = new ChartToolkit();
            engine.SetValue("Chart", chartToolkit);

            // A face as a textured, deformable surface — the third creation route, beside the
            // constructed head and the arranged scene. Ships no mesh: `Mesh.load` reads an OBJ the
            // caller supplies, contained exactly as `outFile` is.
            var meshToolkit = new MeshToolkit(ProjectRoot);
            engine.SetValue("Mesh", meshToolkit);

            // Face landmarks, when an optional backend is installed. Gated on `Face.available` for
            // the reason `Skia.tracer` is: a route that needs landmarks should find out before it
            // spends its passes, not halfway through. Absent on a machine without the venv, which is
            // the ordinary case rather than a fault.
            engine.SetValue("Face", new FaceApi());

            // Characters the GenerateCharacter tool has finished: read-only, like Research, because
            // building one takes minutes and no script can wait for it.
            engine.SetValue("Character", new CharacterToolkit(ProjectRoot));

            // SPIKE: frame capture and animated encoding. Holds bitmaps for the life of the
            // execution, so it is disposed with the engine rather than left to the collector.
            motionToolkit = new MotionToolkit(ProjectRoot);
            engine.SetValue("Motion", motionToolkit);

            // Cloud asset requisition. Registered even when disabled so scripts can branch on the
            // returned failure rather than on the global being absent.
            var assets = Assets ?? new AssetRequisitionToolkit(null, new RequisitionCache(), new AssetBudget(0), "agent");
            engine.SetValue("Assets", assets);

            // Reference photography. Registered on the same terms as Assets, and for a stronger
            // reason: a script that cannot fetch a likeness must be told so, because the failure it
            // would otherwise reach for is inventing an image URL.
            engine.SetValue("Photo", Photos ?? new PhotoToolkit(null, new PhotoBudget(0), "agent"));

            // Reading a supplied document. Same terms again: an unconfigured surface refuses with a
            // remedy rather than being absent.
            engine.SetValue("Documents", Documents ?? new DocumentProcessor(null, new DocumentBudget(0)));

            // Sourced research, read-only. Commissioning is the Research MCP tool's job because a run
            // takes far longer than ScriptTimeoutSeconds allows; by the time a script sees a task the
            // waiting is done, so every member here is a plain read.
            engine.SetValue("Research", new ResearchToolkit(session?.Research ?? new ResearchRegistry()));

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
                (CurrentDrawing.Value?.Canvases ?? canvases).Add(canvas);
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
                (CurrentDrawing.Value?.Papers ?? papers).Add(paper);
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

            // `Snap.brush` is callable *and* a namespace, as `Snap` itself is: `Snap.brush('taper')`
            // is the one-call form and `Snap.brush.taper(20, 1.4)` tunes the same nib. Two separate
            // names would leave an agent guessing which of them takes arguments.
            var brushFunc = new ClrFunction(engine, "brush", (_, args) =>
            {
                var source = args.Length > 0 && !args[0].IsUndefined() ? args[0].ToObject() : null;
                var name = args.Length > 1 && !args[1].IsUndefined() ? args[1].ToString() : "brush";
                return JsValue.FromObject(engine, Snap.Brush(source, name));
            });

            // Bound as explicit functions rather than by handing over the wrapped SnapBrushApi's own
            // members, and both of the obvious shortcuts fail. Enumerating the wrapper's own keys
            // yields none, because a Jint CLR wrapper resolves members lazily — the namespace half
            // came back silently empty, so `Snap.brush('taper')` worked while `Snap.brush.taper(...)`
            // was "not a function". Copying the members across instead reaches the trap already
            // documented for `mina`: called as `Snap.brush.taper(...)`, JS binds `this` to
            // `Snap.brush`, and interop takes that for the CLR receiver — "Object type SnapBrushApi
            // does not match target type Func<...>". A ClrFunction carries its own target, so `this`
            // never enters into it, and this is also where the JS-side defaults live.
            var api = new SnapBrushApi();

            float Num(JsValue[] a, int i, float fallback) => a.Length > i && !a[i].IsUndefined()
                ? Convert.ToSingle(a[i].ToObject(), CultureInfo.InvariantCulture) : fallback;
            int Count(JsValue[] a, int i, int fallback) => a.Length > i && !a[i].IsUndefined()
                ? Convert.ToInt32(a[i].ToObject(), CultureInfo.InvariantCulture) : fallback;
            string Text(JsValue[] a, int i, string fallback) => a.Length > i && !a[i].IsUndefined()
                ? a[i].ToString() : fallback;

            void Nib(string name, Func<JsValue[], object> make) =>
                brushFunc.Set(name, new ClrFunction(engine, name, (_, a) => JsValue.FromObject(engine, make(a))));

            Nib("taper", a => api.Taper(Num(a, 0, 14f), Num(a, 1, 1f), Count(a, 2, 64)));
            Nib("wedge", a => api.Wedge(Num(a, 0, 14f), Num(a, 1, 1f), Count(a, 2, 64)));
            Nib("chisel", a => api.Chisel(Num(a, 0, 12f), Num(a, 1, 18f)));
            Nib("split", a => api.Split(Count(a, 0, 4), Num(a, 1, 16f), Num(a, 2, 1.3f), Count(a, 3, 40)));
            Nib("bristle", a => api.Bristle(Count(a, 0, 24), Num(a, 1, 18f), Num(a, 2, 1f), Count(a, 3, 7)));
            Nib("fromPath", a => api.FromPath(Text(a, 0, string.Empty), Text(a, 1, "brush"), Num(a, 2, 0.75f)));
            Nib("fromElement", a => api.FromElement(
                a.Length > 0 ? a[0].ToObject() as SnapElement ?? throw new ArgumentException(
                    "Snap.brush.fromElement(element) needs an element to take its geometry from.")
                    : throw new ArgumentException("Snap.brush.fromElement(element) needs an element."),
                Text(a, 1, "brush"), Num(a, 2, 0.75f)));
            Nib("preset", a => api.Preset(Text(a, 0, "taper")));
            Nib("hasPreset", a => api.HasPreset(Text(a, 0, string.Empty)));
            brushFunc.Set("presets", JsValue.FromObject(engine, api.Presets));

            snapFunc.Set("brush", brushFunc);

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
                (CurrentDrawing.Value?.Papers ?? papers).Add(paper);
                return JsValue.FromObject(engine, paper);
            }));

            // The reopen half of outSvg, and the reason `svgXml` no longer travels in the response.
            // Registered here rather than on Snap itself because containment needs ProjectRoot, which
            // is the engine's — exactly as Skia.Image.load resolves against the same root.
            snapFunc.Set("load", new ClrFunction(engine, "load", (_, args) =>
            {
                var path = args.Length > 0 && !args[0].IsUndefined() ? args[0].ToString() : string.Empty;
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new ArgumentException("Snap.load(path) needs a path relative to the project directory.");
                }

                // Plain .NET exceptions, as Skia.Image.load throws — Jint surfaces them to the script
                // and the engine's own handler turns them into a failed execution with the message.
                var full = ProjectPath.Resolve(ProjectRoot, path, "path", "Read");
                if (!File.Exists(full))
                {
                    throw new FileNotFoundException(
                        string.IsNullOrEmpty(ProjectRoot) || full.Equals(path, StringComparison.Ordinal)
                            ? $"SVG file not found: {path}"
                            : $"SVG file not found: '{path}' resolves to '{full}'. Paths are relative to the project directory.",
                        full);
                }

                var paper = Snap.Parse(File.ReadAllText(full));
                (CurrentDrawing.Value?.Papers ?? papers).Add(paper);
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

            // A TypeReference for the same reason as CanvasPath below: a ClrFunction has no
            // [[Construct]], so `new ImageData(w, h)` - the only spelling a browser accepts - threw
            // "ImageData is not a constructor". Jint picks the constructor by argument type, and a
            // Uint8ClampedArray converts to the byte[] the data overloads take.
            engine.SetValue("ImageData", TypeReference.CreateTypeReference<ImageData>(engine));

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
            needsAwait = AwaitPattern().IsMatch(jsScript);
            var source = needsAwait
                ? $"(async () => {{\n{jsScript}\n}})();"
                : jsScript;

            // The opener is its own line, so every position Jint reports for a wrapped script is
            // the author's line plus this. Reporting it unadjusted is worse than reporting
            // nothing: an off-by-one sends a reader to the line above the mistake, which in a
            // long file is usually a plausible-looking statement they will then try to fix.

            var evalResult = await engine.EvaluateAsync(source, null, ct);
            sw.Stop();

            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = true;

            SnapPaper? finalPaper = null;
            if (evalResult.ToObject() is SkiaCanvas c)
            {
                result.ReturnValue = c;
                result.ImageBytes = Encode(() => c.ToImageBytes(format, quality));
            }
            else if (evalResult.ToObject() is CanvasRenderingContext2D ctx)
            {
                result.ReturnValue = ctx;
                result.ImageBytes = Encode(() => ctx.Canvas.ToImageBytes(format, quality));
            }
            else if (evalResult.ToObject() is SkiaBitmapWrapper bw)
            {
                result.ReturnValue = bw;
                result.ImageBytes = Encode(() => bw.ToImageBytes(format, quality));
            }
            else if (evalResult.ToObject() is ImageData imgData)
            {
                result.ReturnValue = imgData;
                result.ImageBytes = Encode(() => imgData.ToImageBytes(format, quality));
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
                result.ImageBytes = Encode(() => lastCanvas.ToImageBytes(format, quality));
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
                result.ImageBytes = Encode(() => RenderPaper(finalPaper, defaultWidth, defaultHeight, format, quality));
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
                result.ImageBytes = Encode(() => canvases.Last().ToImageBytes(format, quality));
                if (papers.Count > 0)
                {
                    result.SvgXml = papers.Last().ToString();
                }
            }
            else if (papers.Count > 0)
            {
                var finalPaper = papers.Last();
                result.SvgXml = finalPaper.ToString();
                result.ImageBytes = Encode(() => RenderPaper(finalPaper, defaultWidth, defaultHeight, format, quality));
            }
        }
        catch (PromiseRejectedException prex)
        {
            // An awaited call that threw surfaces here rather than as JavaScriptException, and would
            // otherwise fall through to the generic handler and lose the script-level error text.
            sw.Stop();
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = false;
            result.Error = ExplainRejection(prex, jsScript, needsAwait ? WrapperLines : 0);
            Runtime.Error("JavaScript promise rejected: {0}", result.Error);
        }
        catch (JavaScriptException jsex)
        {
            sw.Stop();
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = false;
            result.Error = Explain(jsex, jsScript);
            Runtime.Error("JavaScript execution error: {0}", result.Error);
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = false;
            result.Error = Explain(ex);
            Runtime.Error(ex, "Script execution error: {0}", ex.Message);
        }

        // Frames are uncompressed bitmaps and can be hundreds of megabytes, so they are released
        // when the execution ends rather than left for the collector to notice.
        motionToolkit?.Dispose();

        // The scratchpad crosses back here, after every catch, because this is the last moment the
        // engine that owns those values is alive.
        if (session is not null) SessionBridge.Settle(sessionEngine, liveSession, session.Storage);

        result.ImageSize = result.ImageBytes?.Length ?? 0;
        result.EncodeTimeMs = encodeSw.ElapsedMilliseconds;
        return result;
    }

    /// <summary>
    /// Turns a runtime failure into something a script author can act on.
    /// </summary>
    /// <remarks>
    /// The statement cap is the case worth special-handling: Jint's own message names neither the
    /// limit nor a remedy, the whole script dies rather than the loop truncating, and an agent
    /// hitting it has no way to tell how far over budget it was. A run reported exactly this —
    /// three scripts lost to it — and noted that the fix is usually a different loop *shape* rather
    /// than less work, since reading each pixel once and classifying it beats one pass per class.
    /// <para>
    /// The second case is a failure the script cannot cause and cannot fix. A missing native library
    /// surfaces as <c>TypeInitializationException</c>, whose message names the type that failed and
    /// nothing else — the reason is in the inner exception, and returning only the outer message
    /// threw it away. A Linux run cost <em>27 tool calls and a compaction</em> to that: told only
    /// "the type initializer for 'SkiaSharp.SKImageInfo' threw an exception", the agent went looking
    /// at fonts, at Snap versus Canvas2D, and at the documentation, for a file that was not on disk.
    /// Naming the cause and saying plainly that no script can work around it is what stops the next
    /// one spending a run the same way.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Turns Jint's bare "cannot access property" into something a script can act on.
    /// </summary>
    /// <remarks>
    /// Reached only when a script has already failed, so the reflection here costs nothing on the
    /// drawing path. Two cases are worth separating, because the raw message conflates them and one
    /// of them reads as a lie: a genuine typo, where the useful thing is the nearest real member; and
    /// an assignment to a member that <i>does</i> exist but is read-only, where "cannot access
    /// property 'width'" invites the reader to conclude that <c>canvas.width</c> is not a thing.
    /// </remarks>
    internal static string ExplainMissingMember(string message)
    {
        var m = MissingMemberMessage().Match(message);
        if (!m.Success) return message;

        var member = m.Groups["member"].Value;

        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(m.Groups["type"].Value, throwOnError: false, ignoreCase: false))
            .FirstOrDefault(t => t is not null);

        var jsType = type?.Name ?? m.Groups["type"].Value;
        if (type is null) return $"{message}'. Check the spelling against polson://sdk/index.";

        // Filtered through the same exclusion the manifest uses, so a suggestion is never a call the
        // reference does not document — proposing `getType` would be worse than proposing nothing.
        var names = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x is PropertyInfo or MethodInfo and not { IsSpecialName: true })
            .Where(x => !JsSurface.NotSurface.Contains(x.Name))
            .Where(x => !x.Name.StartsWith("op_", StringComparison.Ordinal))
            .Select(x => JsSymbolManifest.JsName(x.Name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Same name, different casing or already exact: the member exists and the access was a write
        // to something that has no setter. Jint reports that as unresolved, which is not what it is.
        var actual = names.FirstOrDefault(n => string.Equals(n, member, StringComparison.OrdinalIgnoreCase));
        if (actual is not null)
        {
            return $"'{jsType}.{actual}' is read-only — it can be read but not assigned to, and " +
                "Jint reports that as an inaccessible property. Whatever you were trying to change is " +
                "set another way: a canvas takes its size from createCanvas(w, h), a bitmap from the " +
                "call that produced it. See polson://sdk/index.";
        }

        // Prefix matches first: a name the caller extended or truncated is a likelier fix than one a
        // character or two away, and it must not be crowded out by alphabetical order.
        var near = names.Where(n => IsNearMiss(n, member))
            .OrderByDescending(n => CommonPrefix(n, member))
            .ThenBy(n => Math.Abs(n.Length - member.Length))
            .ThenBy(n => n, StringComparer.Ordinal)
            .Take(5)
            .ToArray();

        if (near.Length > 0)
        {
            return $"'{jsType}' has no property or method '{member}'. Did you mean " +
                string.Join(", ", near.Select(n => $"'{n}'")) + "? A misspelled member is an error " +
                "rather than a new property, so nothing was drawn and nothing was silently ignored.";
        }

        // Nothing close on this receiver, so look across the whole surface. This is the case where a
        // name has been carried in from another library — Skia.RuntimeEffect is CanvasKit's spelling
        // — and the useful answer is not on the type the script reached for.
        var elsewhere = JsSymbolManifest.Symbols
            .Where(s => string.Equals(s.Member, member, StringComparison.OrdinalIgnoreCase) || IsNearMiss(s.Member, member))
            .Select(s => s.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .Take(4)
            .ToArray();

        var pointer = elsewhere.Length > 0
            ? " There is no such member here, but " + string.Join(", ", elsewhere.Select(n => $"'{n}'"))
                + " exists elsewhere on the surface."
            : string.Empty;

        return $"'{jsType}' has no property or method '{member}'.{pointer} A misspelled member is an " +
            "error rather than a new property, so nothing was drawn and nothing was silently ignored. " +
            "Read polson://sdk/index for the exact spelling.";
    }

    /// <summary>How many leading characters two names share, ignoring case. The ranking key.</summary>
    /// <remarks>
    /// Longest shared prefix first, so <c>fillStyle</c> beats <c>fill</c> for a mistyped
    /// <c>fillStlye</c> — both are plausible, and the one that diverges latest is the one meant.
    /// </remarks>
    private static int CommonPrefix(string a, string b)
    {
        var n = 0;
        while (n < a.Length && n < b.Length &&
               char.ToLowerInvariant(a[n]) == char.ToLowerInvariant(b[n])) n++;
        return n;
    }

    /// <summary>Whether one name is the other with something added or removed at the end.</summary>
    /// <remarks>
    /// The commonest way a real call is mistyped, and the one a length window rejects. A live run
    /// asked for <c>perlinNoiseFractalNoise</c>: the correct <c>perlinNoiseFractal</c> is five
    /// characters shorter and was excluded, while <c>perlinNoiseTurbulence</c> — a different function
    /// — fell inside the window and was suggested instead.
    /// </remarks>
    private static bool SharesAffix(string candidate, string typed) =>
        candidate.Length >= 4 && typed.Length >= 4 &&
        (candidate.StartsWith(typed, StringComparison.OrdinalIgnoreCase) ||
         typed.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));

    /// <summary>A typo, rather than a different call: extended, truncated, or one edit away.</summary>
    /// <summary>Whether a script's read of <paramref name="member"/> on <paramref name="target"/> finds something.</summary>
    /// <remarks>
    /// On a list that is its own declared members, an index or <c>length</c>, or anything
    /// <c>Array.prototype</c> carries, because the engine attaches that prototype to a list. Anywhere
    /// else it is <see cref="MemberIndex.Has"/>. One answer for the accessor, <c>has</c> and
    /// <c>suggest</c>, so the three cannot disagree about the same name.
    /// </remarks>
    private static bool Resolves(Engine engine, object? target, string member) =>
        IsList(target)
            ? MemberIndex.Declares(target!, member)
              || IsIndexOrLength(member)
              || ((ObjectInstance)engine.Intrinsics.Array.Get("prototype")).HasProperty(member)
            : MemberIndex.Has(target, member);

    /// <summary>A collection the engine wraps as a list: enumerable, and neither a string nor a dictionary.</summary>
    private static bool IsList(object? target) => target is IEnumerable and not string and not IDictionary;

    /// <summary>An index or <c>length</c>: the reads a list answers itself, whatever its type declares.</summary>
    private static bool IsIndexOrLength(string member) =>
        member == "length" || (member.Length > 0 && member.All(char.IsAsciiDigit));

    private static bool IsNearMiss(string candidate, string typed)
    {
        if (SharesAffix(candidate, typed)) return true;

        if (Math.Abs(candidate.Length - typed.Length) > 2) return false;
        if (candidate.Length >= 4 && typed.Length >= 4 &&
            candidate.StartsWith(typed[..3], StringComparison.OrdinalIgnoreCase)) return true;

        // Transposition and single-substitution cover almost every real mistyping.
        var differences = 0;
        for (var i = 0; i < Math.Min(candidate.Length, typed.Length); i++)
        {
            if (!char.ToLowerInvariant(candidate[i]).Equals(char.ToLowerInvariant(typed[i]))) differences++;
            if (differences > 2) return false;
        }
        return true;
    }

    [GeneratedRegex(@"Cannot access property '(?<member>[^']+)' on type '(?<type>[^']+)'?")]
    private static partial Regex MissingMemberMessage();

    internal static string Explain(Exception ex, string? script = null)
    {
        var message = ex.Message ?? string.Empty;

        if (ex is JavaScriptException js)
        {
            return $"JavaScript error: {message}{Where(js)}"
                + ArgumentHelp(message, SourceLine(script, js.Location.Start.Line))
                + NotCallableHelp(message, script);
        }

        if (ex is MissingMemberException) return ExplainMissingMember(message);

        // Anywhere in the chain: the load failure is usually two or three levels under whatever the
        // engine surfaced, and which level it sits at is not worth depending on.
        for (var cause = ex; cause is not null; cause = cause.InnerException)
        {
            if (cause is DllNotFoundException or BadImageFormatException)
            {
                return $"{message} The underlying failure is: {cause.Message} " +
                    "A native library the drawing engine depends on is missing or unusable for this " +
                    "platform, which is a problem with how the server was built or deployed rather " +
                    "than with this script. No script can work around it and every drawing call will " +
                    "fail the same way, so stop and report it rather than trying another API.";
            }
        }

        // Any other initializer failure: at least say why, instead of only which type.
        if (ex is TypeInitializationException && ex.InnerException is { } reason)
        {
            return $"{message} The reason is: {reason.Message}";
        }

        // Jint's own time limit throws a bare TimeoutException, whose message names nothing: on a live
        // run it read as the network, and fourteen minutes went on the wrong hypothesis.
        if (ex is TimeoutException || ex.InnerException is TimeoutException)
        {
            return $"{message} This is the script's {ScriptTimeoutSeconds}-second limit, not a network " +
                "timeout: the script was stopped and nothing it drew was kept. Awaiting a requisition " +
                "counts toward it, so requisition in one script and draw in the next.";
        }

        if (!message.Contains("maximum number of statements", StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        return $"{message} The limit is {MaxStatements:N0} statements and the whole script is " +
            "abandoned when it is reached, so nothing it drew was kept. A per-pixel loop costs " +
            "roughly 15-40 statements per iteration, which puts the practical budget near 100,000 " +
            "sampled pixels. Sample at a stride (every 4th or 6th pixel), read each pixel once and " +
            "classify it in that single pass rather than looping the image once per colour, or move " +
            "the work off the interpreter entirely: bitmap.diff(...), bitmap.rowProfile(...) and " +
            "bitmap.palette(...) measure natively, and Skia.Shader / Skia.ImageFilter transform " +
            "natively.";
    }

    /// <summary>A rejected promise, explained as fully as a thrown error is.</summary>
    /// <remarks>
    /// <para>
    /// <b>This exists because the diagnostics were reaching exactly the wrong half of the scripts.</b>
    /// Jint surfaces anything thrown inside an <c>await</c> as a <see cref="PromiseRejectedException"/>
    /// rather than a <see cref="JavaScriptException"/>, and the first version of that catch stopped at
    /// the message. So <see cref="Where"/>, <see cref="ArgumentHelp"/> and <see cref="NotCallableHelp"/>
    /// were skipped for **every script using top-level await** — which is every script that
    /// requisitions an asset or reads a document, and therefore the normal shape of an infographic
    /// run rather than an edge case.
    /// </para>
    /// <para>
    /// Measured on a live run: <c>timelineModel.scale(yr)</c> — the d3 habit
    /// <see cref="NotCallableHelp"/> was written for — cost a script and got back
    /// "Promise was rejected with value TypeError: scale is not a function", with no line and no
    /// advice, from a 566-line file. The hint had been written, tested, and was unreachable.
    /// </para>
    /// <para>
    /// The wrapping also defeats the matching itself: every pattern here is anchored at the start of
    /// the message, and the rejected text arrives behind "Promise was rejected with value TypeError: ".
    /// So the value is unwrapped to the inner message before anything is asked of it.
    /// </para>
    /// </remarks>
    internal static string ExplainRejection(PromiseRejectedException ex, string? script = null,
        int lineOffset = 0)
    {
        var (message, reported) = Rejected(ex.RejectedValue);
        var line = reported > lineOffset ? reported - lineOffset : 0;

        // A non-Error rejection — `Promise.reject('gave up')` — has no message of its own, so the
        // exception's own text is the only account of it there is.
        if (string.IsNullOrEmpty(message)) return $"JavaScript error: {ex.Message}";

        return $"JavaScript error: {message}{(line > 0 ? $" (line {line})" : string.Empty)}"
            + ArgumentHelp(message, SourceLine(script, line))
            + NotCallableHelp(message, script);
    }

    /// <summary>The message and line of a rejected value, when it is an Error object.</summary>
    private static (string Message, int Line) Rejected(JsValue value)
    {
        if (value is not ObjectInstance error) return (string.Empty, 0);

        var text = error.Get("message");
        var message = text.IsString() ? text.AsString() : string.Empty;
        if (string.IsNullOrEmpty(message)) return (string.Empty, 0);

        // The line comes from the Error's own stack rather than from a Location, because a rejection
        // carries no position: by the time it is observed the frame that threw has gone. Best effort
        // on purpose — a missing line costs the "(line n)" suffix and nothing else, where guessing a
        // wrong one would send a reader to the wrong place in a long file.
        var line = 0;
        var stack = error.Get("stack");
        if (stack.IsString() && StackLine().Match(stack.AsString()) is { Success: true } hit)
        {
            _ = int.TryParse(hit.Groups["line"].Value, out line);
        }

        return (message, line);
    }

    [GeneratedRegex(@":(?<line>\d+):\d+")]
    private static partial Regex StackLine();

    /// <summary>Where in the script it happened, when Jint recorded a position.</summary>
    private static string Where(JavaScriptException ex)
    {
        var line = ex.Location.Start.Line;
        return line > 0 ? $" (line {line})" : string.Empty;
    }

    /// <summary>
    /// Jint's overload-resolution failure, which names neither the method nor the argument.
    /// </summary>
    /// <remarks>
    /// "No public methods with the specified arguments were found" is what a script gets for passing
    /// an argument of the wrong type, and by far its commonest cause is <c>undefined</c> from a
    /// property that does not exist. A live run lost a full 97-line composition to
    /// <c>rect.w</c> — the toolkit's rectangles carry <c>width</c> and <c>height</c> — which made
    /// <c>ctx.fillRect(x, y, undefined, undefined)</c>, and the message said none of that.
    /// <para>
    /// The line number from <see cref="Where"/> is what actually locates it; this adds the reading
    /// that turns a located line into a fixed one.
    /// </para>
    /// </remarks>
    private static string ArgumentHelp(string message, string? sourceLine = null)
    {
        if (!message.Contains("No public methods with the specified arguments", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        // A call whose arguments are simply the wrong way round reaches the same message, and the
        // generic advice below then sends the reader hunting for a misspelled property that is not
        // there. A live run lost a script to `Stage.check(barW > 0, 'positive baseline')` and spent
        // its next step looking for a typo in Layout, because that is what the message told it to do.
        if (SwappedCheck().IsMatch(sourceLine ?? string.Empty))
        {
            return " Stage.check takes the claim first and the verdict second — " +
                "Stage.check('what you are asserting', measuredValue > 0) — and this call has them " +
                "the other way round. Written that way the boolean becomes the claim and the message " +
                "becomes the verdict, so the check could only ever pass; the signature refuses it " +
                "rather than recording something that cannot fail.";
        }

        // The D3 reflex. `Scale` takes four scalars where d3 takes .domain([a,b]).range([c,d]), so a
        // caller carrying that habit passes arrays and gets told to hunt for an undefined property.
        // A live run lost a script to it and spent its next step on a Search to recover the shapes.
        if (D3ScalePair().IsMatch(sourceLine ?? string.Empty))
        {
            return " Scale takes plain numbers, not d3-style [min, max] pairs — " +
                "Scale.linear(domainStart, domainEnd, rangeStart, rangeEnd) and " +
                "Scale.band(count, rangeStart, rangeEnd, padding), each argument its own value. " +
                "There is no .domain(...)/.range(...) here: a scale is built in one call and the " +
                "range may run backwards, which is how a vertical axis puts larger values higher. " +
                "Spread the arrays into their elements.";
        }

        // Wrong *count* reaches the same message as wrong *type*, and the advice below is about type.
        // Checked before it, because when the count is wrong that advice is not merely unhelpful but
        // misdirecting — see the remark on ArityHelp.
        if (ArityHelp(sourceLine) is { Length: > 0 } arity) return arity;

        return " An argument is not a type the method accepts, and the commonest reason is that one of " +
            "them is undefined — reading a property that does not exist yields undefined rather than " +
            "failing, and no overload matches it. Check the spelling of every property read on that " +
            "line: rectangles from Layout, getBBox and measureWrappedText carry width and height, " +
            "not w and h. Logging the arguments before the call is the quickest way to see which one. " +
            "If nothing on the line is undefined, count the arguments — the same message covers a " +
            "call given the wrong number of them.";
    }

    /// <summary>A member call on this line given a number of arguments no overload accepts.</summary>
    /// <remarks>
    /// <b>The generic advice is about the wrong thing when the count is wrong, and a run followed it
    /// into a wall.</b> A script wrote <c>paper.line(cx, cy - 10, cx, cy + 10, cy)</c> — five
    /// arguments to a four-argument method, a stray trailing value — and was told to check the
    /// spelling of every property on the line and to watch for <c>w</c> and <c>h</c> on Layout
    /// rectangles. There was no property, no misspelling and no Layout rectangle. The agent checked
    /// what it was told to check, found nothing, and <b>re-ran the byte-identical script</b>; four
    /// scripts went that way before it moved on.
    /// <para>
    /// So this reports the one thing the message never carries: how many arguments the line actually
    /// passes, against how many the surface accepts. The expected count is read from
    /// <see cref="JsSymbolManifest"/> — the same index <c>suggest</c> searches — so it cannot drift
    /// from the real signatures.
    /// </para>
    /// <para>
    /// <b>Member calls only.</b> A bare <c>draw(a, b, c)</c> is far more likely to be the script's own
    /// helper than an SDK global of the same name, and reporting a stranger's arity at it would be
    /// the same confident wrong answer this exists to remove.
    /// </para>
    /// </remarks>
    private static string ArityHelp(string? sourceLine)
    {
        if (string.IsNullOrWhiteSpace(sourceLine)) return string.Empty;

        for (var i = 0; i < sourceLine.Length; i++)
        {
            if (sourceLine[i] != '(') continue;

            var end = i;
            while (end > 0 && char.IsWhiteSpace(sourceLine[end - 1])) end--;

            var start = end;
            while (start > 0 && (char.IsLetterOrDigit(sourceLine[start - 1]) || sourceLine[start - 1] == '_'))
            {
                start--;
            }

            if (start == end || start == 0 || sourceLine[start - 1] != '.') continue;

            var passed = CountArguments(sourceLine, i);
            if (passed < 0) continue;

            var name = sourceLine[start..end];
            var accepted = AcceptedArities(name);
            if (accepted.Count == 0 || accepted.Any(a => passed >= a.Min && passed <= a.Max)) continue;

            var shapes = string.Join(" or ", accepted
                .Select(a => a.Min == a.Max
                    ? a.Min.ToString(CultureInfo.InvariantCulture)
                    : $"{a.Min}-{a.Max}")
                .Distinct(StringComparer.Ordinal));

            return $" This line passes {passed} arguments to '{name}', which accepts {shapes}. " +
                "A call given the wrong number of arguments reports the same failure as one given the " +
                "wrong type, so count them before hunting for a misspelled property — a stray " +
                "trailing value is the usual cause, and it reads perfectly.";
        }

        return string.Empty;
    }

    /// <summary>How many arguments the call opening at <paramref name="open"/> is given, or -1.</summary>
    /// <remarks>
    /// Depth-aware, because an argument may itself be a call, an array, an object literal or a string
    /// holding commas — counting every comma would report a nested literal as extra arguments, which
    /// is exactly the confident wrong answer this file is trying to stop making. -1 when the call does
    /// not close on this line, since a count over a fragment is worse than no count at all.
    /// </remarks>
    private static int CountArguments(string line, int open)
    {
        const char Apostrophe = (char)39;
        const char Backtick = (char)96;

        var depth = 0;
        var commas = 0;
        var anything = false;
        var quote = (char)0;

        for (var i = open; i < line.Length; i++)
        {
            var c = line[i];

            if (quote != (char)0)
            {
                if (c == quote) quote = (char)0;
                continue;
            }

            if (c == '"' || c == Apostrophe || c == Backtick)
            {
                quote = c;
                anything = true;
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                depth++;
                if (depth > 1) anything = true;
                continue;
            }

            if (c is ')' or ']' or '}')
            {
                depth--;
                if (depth == 0) return anything ? commas + 1 : 0;
                continue;
            }

            if (c == ',' && depth == 1) { commas++; continue; }
            if (!char.IsWhiteSpace(c)) anything = true;
        }

        return -1;
    }

    /// <summary>The argument counts every documented overload of <paramref name="member"/> accepts.</summary>
    /// <remarks>
    /// Read from the manifest's own signatures rather than a table kept here, so a method that gains a
    /// parameter cannot leave this advertising the old arity. Optionality is marked before the colon,
    /// which is what separates the minimum from the maximum.
    /// </remarks>
    private static List<(int Min, int Max)> AcceptedArities(string member)
    {
        var shapes = new List<(int Min, int Max)>();

        foreach (var symbol in JsSymbolManifest.Symbols)
        {
            if (!string.Equals(symbol.Member, member, StringComparison.Ordinal)) continue;

            var open = symbol.Signature.IndexOf('(');
            var close = symbol.Signature.LastIndexOf(')');
            if (open < 0 || close <= open) continue;

            var inside = symbol.Signature[(open + 1)..close].Trim();
            if (inside.Length == 0) { shapes.Add((0, 0)); continue; }

            var parts = inside.Split(',');
            var required = parts.Count(part =>
            {
                var colon = part.IndexOf(':');
                return !(colon < 0 ? part : part[..colon]).Contains('?');
            });

            shapes.Add((required, parts.Length));
        }

        return [.. shapes.Distinct()];
    }

    /// <summary>The source of one 1-based line, when the script is to hand.</summary>
    private static string? SourceLine(string? script, int line)
    {
        if (string.IsNullOrEmpty(script) || line <= 0) return null;

        var lines = script.Split('\n');
        return line <= lines.Length ? lines[line - 1] : null;
    }

    /// <summary>
    /// <c>Stage.check(...)</c> whose first argument is plainly not a claim string.
    /// </summary>
    /// <remarks>
    /// Matches on the first argument being unquoted, which is what a boolean expression looks like.
    /// A template literal is quoted by backtick and so is correctly left alone.
    /// </remarks>
    [GeneratedRegex(@"Stage\s*\.\s*check\s*\(\s*[^'""`\s)]", RegexOptions.IgnoreCase)]
    private static partial Regex SwappedCheck();

    /// <summary>
    /// A <c>Scale</c> call carrying an array <i>literal</i> where a number belongs.
    /// </summary>
    /// <remarks>
    /// The bracket must open an argument — directly after the <c>(</c>, or directly after a comma —
    /// so that an ordinary <b>index</b> is not mistaken for a d3 pair. <c>Scale.linear(0, data[i], a,
    /// b)</c> is a perfectly good call and must not be told it is d3; the character class excludes
    /// <c>[</c>, so the match cannot run past one to find a later bracket.
    /// </remarks>
    [GeneratedRegex(@"Scale\s*\.\s*\w+\s*\(\s*(?:[^()\[\]]*,\s*)?\[", RegexOptions.IgnoreCase)]
    private static partial Regex D3ScalePair();

    /// <summary>
    /// "x is not a function", when <c>x</c> holds a toolkit object that is not callable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A d3 habit, and a reasonable one.</b> Every charting library a script author has met makes
    /// a scale <i>callable</i> — <c>scale(value)</c> — and ours returns an object whose mapping is
    /// <c>scale.map(value)</c>. A live run wrote <c>scaleAlt(alt)</c> and lost its script, having
    /// already used <c>scaleAlt.isZeroBased</c> correctly two lines earlier: it knew the shape and
    /// reverted to muscle memory for the common call.
    /// </para>
    /// <para>
    /// Jint answers "scaleAlt is not a function", which is true and names nothing to do about it. The
    /// script is to hand, so the variable's producer can be found and the right member named.
    /// </para>
    /// </remarks>
    private static string NotCallableHelp(string message, string? script)
    {
        if (string.IsNullOrEmpty(script) || NotAFunction().Match(message) is not { Success: true } m)
        {
            return string.Empty;
        }

        var name = m.Groups["name"].Value;
        var assigned = Regex.Match(script,
            @"\b(?:const|let|var)\s+" + Regex.Escape(name) + @"\s*=\s*(?<producer>Scale|Chart|Layout|Snap)\s*\.\s*(?<call>\w+)");
        if (!assigned.Success) return string.Empty;

        var producer = assigned.Groups["producer"].Value;
        var call = assigned.Groups["call"].Value;
        var advice = producer switch
        {
            "Scale" => $"`{name}.map(value)` maps a value to a pixel; `.invert(position)` goes back, and "
                     + "`.extent(from, to)` is the distance between two values. A band scale uses "
                     + $"`{name}.map(index)`, `{name}.center(index)` and `{name}.bandwidth`.",
            "Chart" => $"a chart model is data: read `{name}.slots`, `{name}.ticks` and `{name}.labels`, "
                     + $"and draw with `Chart.drawChart(ctx, {name})` or `paper.chart({name})`.",
            "Layout" => $"a rectangle is data: read `{name}.x`, `{name}.y`, `{name}.width`, `{name}.height`, "
                      + $"`{name}.x2`, `{name}.y2`, `{name}.cx`, `{name}.cy`.",
            _ => $"read its members rather than calling it; see polson://sdk/core/{producer}.",
        };

        return $" `{name}` came from `{producer}.{call}(...)`, which returns an object rather than a "
             + $"function — unlike d3, where a scale is callable. {advice}";
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z_$][\w$]*) is not a function")]
    private static partial Regex NotAFunction();

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
    /// <summary>Lines the async wrapper adds above the script, when one is used.</summary>
    /// <remarks>
    /// A named constant because two places depend on it and they sit far apart: the wrapping
    /// itself, and the arithmetic that turns a reported position back into the author's own
    /// numbering. They cannot drift while both read this.
    /// </remarks>
    private const int WrapperLines = 1;

    [System.Text.RegularExpressions.GeneratedRegex(@"\bawait\s")]
    private static partial System.Text.RegularExpressions.Regex AwaitPattern();
    #endregion
}

