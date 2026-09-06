namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Polson.ExtendedMind.ImageGeneration;
using Polson.ExtendedMind.ParallelSearch;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;

/// <summary>One callable or readable name on the JavaScript surface.</summary>
public sealed record JsSymbol(
    string Receiver,
    string Member,
    string Kind,
    string Area,
    string Uri,
    string Signature,
    string ClrType,
    string ClrMember,
    bool Inherited)
{
    /// <summary>The name as a script writes it, e.g. <c>paper.squircle</c>.</summary>
    public string Name => Receiver.Length == 0 ? Member : $"{Receiver}.{Member}";
}

/// <summary>A JS name that members hang off — a global namespace, or an instance the docs give a conventional name.</summary>
/// <remarks>
/// Several CLR types can share one receiver name. The reference documents <c>gradient</c> for all
/// four gradient types, so a single-type mapping would report nine real members as missing.
/// </remarks>
public sealed record JsReceiver(string Name, Type[] Types, string Area, bool IsInstance)
{
    public JsReceiver(string name, Type type, string area, bool isInstance)
        : this(name, [type], area, isInstance) { }
}

/// <summary>
/// The declared JavaScript surface: which CLR types back which JS names.
/// </summary>
/// <remarks>
/// <para>
/// Receiver names are a documentation convention, not an engine fact — nothing in the runtime says
/// that <c>paper</c> means <see cref="SnapPaper"/>. That mapping has to be declared, and this is
/// where. Everything else about a symbol is derived by reflection, so signatures cannot drift.
/// </para>
/// <para>
/// The globals list, by contrast, <b>is</b> an engine fact and is not declared here: it is read back
/// out of a live engine by <see cref="JsSymbolManifest.RuntimeGlobals"/>, so a registration added to
/// <see cref="JsDrawingEngine"/> without a matching receiver is caught by test rather than by a
/// confused agent months later.
/// </para>
/// </remarks>
public static class JsSurface
{
    #region Properties
    public static IReadOnlyList<JsReceiver> Receivers { get; } =
    [
        new("console", typeof(JsConsole), "Globals", false),
        new("mina", typeof(Mina), "Globals", false),
        new("Stage", typeof(StageApi), "Globals", false),

        new("Snap", typeof(Snap), "Snap", false),
        new("Snap.path", typeof(SnapPathApi), "Snap", false),
        new("paper", typeof(SnapPaper), "Snap", true),
        new("element", typeof(SnapElement), "Snap", true),
        new("matrix", typeof(SnapMatrix), "Snap", true),
        new("gradient", [typeof(SnapGradient), typeof(SnapLinearGradient), typeof(SnapRadialGradient), typeof(CanvasGradient)], "Snap", true),

        new("canvas", typeof(SkiaCanvas), "Canvas2D", true),
        new("ctx", typeof(CanvasRenderingContext2D), "Canvas2D", true),
        new("path", typeof(CanvasPath), "Canvas2D", true),

        new("Skia", typeof(SkiaApi), "Skia", false),
        new("Skia.Shader", typeof(SkiaShaderApi), "Skia", false),
        new("Skia.ImageFilter", typeof(SkiaImageFilterApi), "Skia", false),
        new("Skia.ColorFilter", typeof(SkiaColorFilterApi), "Skia", false),
        new("Skia.PathEffect", typeof(SkiaPathEffectApi), "Skia", false),
        new("Skia.MaskFilter", typeof(SkiaMaskFilterApi), "Skia", false),
        new("Skia.Brush", typeof(SkiaBrushApi), "Skia", false),
        new("brush", typeof(BrushPreset), "Skia", true),
        new("Skia.Image", typeof(SkiaImageApi), "Skia", false),
        new("Skia.Bitmap", typeof(SkiaBitmapFactoryApi), "Skia", false),
        new("Skia.Font", typeof(SkiaFontApi), "Skia", false),
        new("bitmap", typeof(SkiaBitmapWrapper), "Skia", true),
        new("imageData", typeof(ImageData), "Skia", true),

        new("Assets", typeof(AssetRequisitionToolkit), "Assets", false),
        new("material", typeof(MaterialAsset), "Assets", true),
        new("plate", typeof(BackdropPlate), "Assets", true),
        new("matte", typeof(MatteAsset), "Assets", true),
        new("budget", typeof(AssetBudget), "Assets", true),

        // Read-only. Research is commissioned by the Research MCP tool, never from a script.
        new("Research", typeof(ResearchToolkit), "Research", false),
        new("task", typeof(ResearchTask), "Research", true),
        new("basis", typeof(FieldBasis), "Research", true),
        new("citation", typeof(TaskCitation), "Research", true),

        new("Drawing", typeof(ConstructiveDrawingToolkit), "Drawing", false),
        new("Logo", typeof(LogoDesignToolkit), "Logo", false),
        new("VectorLogo", typeof(VectorLogoToolkit), "VectorLogo", false),
        new("LogoType", typeof(LogoTypeToolkit), "LogoType", false),
        new("Layout", typeof(LayoutToolkit), "Layout", false),

        new("Css", typeof(Polson.HtmlParser.CssToolkit), "Css", false),
        new("sheet", typeof(Polson.HtmlParser.StyleSheetView), "Css", true),

        new("Scale", typeof(ScaleToolkit), "Scale", false),
        new("scale", typeof(LinearScale), "Scale", true),
        new("band", typeof(BandScale), "Scale", true),

        // Constructions above those primitives. Its own area rather than more of Scale, because
        // Scale is deliberately "draws nothing and knows no forms" and that is worth keeping.
        new("Chart", typeof(ChartToolkit), "Chart", false),

        // SPIKE: frame capture and animated encoding. Declared here so the manifest stays honest
        // about what a script can reach, not because the surface is settled.
        new("Motion", typeof(MotionToolkit), "Motion", false),
        new("tl", typeof(MotionTimeline), "Motion", true),
    ];

    /// <summary>
    /// Members that exist publicly on a JS-reachable type but are not part of the surface an agent
    /// should use: object plumbing, disposal, and Jint's own interop hooks.
    /// </summary>
    public static IReadOnlySet<string> NotSurface { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Equals", "GetHashCode", "GetType", "Finalize",
        "GetProp", "InvokeCallback", "Deconstruct",
        "<Clone>$",   // compiler-generated record copy helper, not a JS member
    };

    /// <summary>
    /// Members a script can reach but that the reference deliberately omits, each with its reason.
    /// </summary>
    /// <remarks>
    /// Shared with <c>ApiDocumentationTests</c> so the drift test and the symbol manifest agree on
    /// what the supported surface is. A second copy of this list would be a second thing to forget.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> Excluded { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
            // Internal plumbing that happens to be public.
            ["console.formatArgs"] = "internal log formatting helper",
            ["console.logs"] = "internal log buffer; console.clear is the documented control",
            ["Skia.ColorFilter.parseBlendMode"] = "internal blend-mode string parser",
            ["Snap.version"] = "build stamp, not a drawing call",
            ["Snap.color"] = "returns a .NET Color, not a JS value; Snap.rgb / Snap.hsl are the JS route",
            ["Snap.logo"] = "accessor for the global VectorLogo toolkit",
            ["Snap.vectorLogo"] = "accessor for the global VectorLogo toolkit",
            ["paper.ensureDefs"] = "internal defs bootstrap; paper.defs is the documented accessor",
            ["paper.snapPaper"] = "constructor artifact, not a callable",
            ["element.wrap"] = "internal element wrapper factory",
            // Aliases of a documented member; documenting both spellings invites drift.
            ["paper.toDataURL"] = "alias of paper.toDataUri",
            ["bitmap.toDataURL"] = "alias of bitmap.toDataUri",
            ["imageData.toDataURL"] = "alias of imageData.toDataUri",
            ["canvas.toDataURL"] = "alias of canvas.toDataUri",
            ["paper.logo"] = "alias of paper.vectorLogo",
            ["paper.vectorLogo"] = "per-paper accessor for the global VectorLogo toolkit",
            // Typed .NET escape hatches with no JS-facing contract.
            ["paper.document"] = "raw Svg.NET document; not part of the JS surface",
            ["element.node"] = "raw Svg.NET node; not part of the JS surface",
            ["canvas.skCanvas"] = "raw SkiaSharp canvas; not part of the JS surface",
            ["canvas.skBitmap"] = "raw SKBitmap behind canvas.bitmap; not part of the JS surface",
            ["path.path"] = "raw SkiaSharp path behind a CanvasPath; not part of the JS surface",
            ["bitmap.bitmap"] = "raw SKBitmap; not part of the JS surface",
            ["gradient.shader"] = "raw SKShader; not part of the JS surface",
            ["gradient.createShader"] = "internal: builds the SKShader when the gradient is used as a fill",
            ["gradient.gradientNode"] = "raw Svg.NET paint server; not part of the JS surface",
            ["gradient.linearNode"] = "raw Svg.NET paint server; not part of the JS surface",
            ["gradient.radialNode"] = "raw Svg.NET paint server; not part of the JS surface",
            ["gradient.startPoint"] = "Canvas2D gradient construction detail, fixed at creation",
            ["gradient.endPoint"] = "Canvas2D gradient construction detail, fixed at creation",
            ["gradient.startRadius"] = "Canvas2D gradient construction detail, fixed at creation",
            ["gradient.endRadius"] = "Canvas2D gradient construction detail, fixed at creation",
            ["gradient.startAngle"] = "Canvas2D conic gradient construction detail, fixed at creation",
            ["gradient.type"] = "inherited SnapElement tag name; documented on element",
            ["ctx.canvas"] = "back-reference to the owning canvas",
            ["matrix.toSkMatrix"] = "raw SKMatrix; not part of the JS surface",
            ["matrix.toSvgMatrix"] = "raw Svg.NET matrix; not part of the JS surface",
            ["Logo.getProp"] = "interop helper for reading JS option objects",
            ["Logo.invokeCallback"] = "interop helper for calling a JS mark function",
            // Lifetime management, not drawing.
            ["bitmap.dispose"] = "documented in the Skia area prose rather than as a call",
            ["canvas.dispose"] = "lifetime management, not a drawing call",
            ["Motion.dispose"] = "the engine releases held frames when the execution ends; Motion.clear is the script-facing control",
            ["paper.saveImage"] = "server-side file write; ExecuteScript outFile is the documented route",
            ["canvas.saveImage"] = "server-side file write; ExecuteScript outFile is the documented route",
            ["bitmap.saveImage"] = "server-side file write; ExecuteScript outFile is the documented route"
    };


    /// <summary>Globals that are bare functions rather than namespaces, so they have no members to enumerate.</summary>
    public static IReadOnlySet<string> FunctionGlobals { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "log", "error", "exit", "table", "createCanvas", "Canvas", "ImageData", "Snap", "Session", "SK",
        "CanvasPath", "Path2D",

        // `has(object, name)` — whether a member exists, without touching it. A function rather than
        // an idiom because resolution here is strict, so `typeof`, `in`, `Object.hasOwn` and
        // `Reflect.has` all throw instead of answering. See `MemberIndex`.
        "has", "suggest",
    };
    #endregion
}

/// <summary>
/// Builds the machine-readable index of the JavaScript surface, by reflection over
/// <see cref="JsSurface"/>.
/// </summary>
/// <remarks>
/// This exists because the hand-written reference cannot be parsed reliably enough to answer
/// "does this call exist?". Prose carries several symbols per line, documents some only in
/// middle-dot runs, and lost an entire area to a heading-text mismatch. A similarity search over
/// that text can never return a confident negative; this index can, because a name is either in it
/// or it is not.
/// </remarks>
public static partial class JsSymbolManifest
{
    #region Properties
    public static IReadOnlyList<JsSymbol> Symbols { get; } = Build();
    #endregion

    #region Methods
    public static IReadOnlyList<JsSymbol> Build()
    {
        var symbols = new List<JsSymbol>();

        // Which receiver name a type is registered under, so an exclusion written against the type
        // that declares a member can be found again from every receiver that inherits it.
        var receiverOf = JsSurface.Receivers
            .SelectMany(r => r.Types.Select(t => (Type: t, r.Name)))
            .GroupBy(x => x.Type)
            .ToDictionary(g => g.Key, g => g.First().Name);

        foreach (var receiver in JsSurface.Receivers)
        {
            var flags = BindingFlags.Public | BindingFlags.DeclaredOnly |
                        (receiver.IsInstance ? BindingFlags.Instance : BindingFlags.Instance | BindingFlags.Static);

            // Jint resolves inherited members too, so enumerating only the declaring type would hide
            // everything a SnapGradient gets from SnapElement.
            var chain = receiver.Types.SelectMany(Ancestry).Distinct();

            foreach (var member in chain.SelectMany(t => t.GetMembers(flags)))
            {
                if (member is not (MethodInfo or PropertyInfo)) continue;
                if (member is MethodInfo { IsSpecialName: true }) continue;
                if (JsSurface.NotSurface.Contains(member.Name)) continue;
                if (JsSurface.Excluded.ContainsKey($"{receiver.Name}.{JsName(member.Name)}")) continue;
                if (member.Name.StartsWith("op_", StringComparison.Ordinal)) continue;

                // An exclusion follows the member it was written for down the inheritance chain.
                // `element.node` is excluded as a raw Svg.NET escape hatch, but a paper and a
                // gradient are both SnapElements, so paper.node and gradient.node were published as
                // SDK calls — the same escape hatch under two names nobody had thought to exclude.
                if (member.DeclaringType is { } declaring
                    && receiverOf.TryGetValue(declaring, out var declaringReceiver)
                    && declaringReceiver != receiver.Name
                    && JsSurface.Excluded.ContainsKey($"{declaringReceiver}.{JsName(member.Name)}")) continue;

                // A namespace accessor is one the registry above names as a receiver in its own
                // right — "Skia.Shader" is an entry, so Skia.Shader keeps its capital. Its
                // registered spelling is authoritative, which is what makes Snap.path lowercase
                // while Skia.Shader is not; both are how the reference writes them.
                //
                // The previous rule was "any property whose type is a receiver", which is a
                // different question and gave the wrong answer for every property that merely
                // *returns* one: element.paper, paper.defs, canvas.bitmap and Assets.budget were
                // published as element.Paper, paper.Defs, canvas.Bitmap and Assets.Budget —
                // capitalised names the reference does not document and an agent would not type.
                var namespaceReceiver = JsSurface.Receivers.FirstOrDefault(r =>
                    string.Equals(r.Name, $"{receiver.Name}.{member.Name}", StringComparison.OrdinalIgnoreCase));

                var jsName = namespaceReceiver is not null
                    ? namespaceReceiver.Name[(receiver.Name.Length + 1)..]
                    : JsName(member.Name);

                // Where a call is documented is not always its receiver's home area: paper.squircle
                // is declared on SnapPaper but written up under VectorLogo. Pointing an agent at the
                // receiver's area would hand it a URI that does not contain the call it asked about.
                var area = DocumentedArea($"{receiver.Name}.{jsName}") ?? receiver.Area;

                symbols.Add(new JsSymbol(
                    receiver.Name,
                    jsName,
                    member is MethodInfo ? "method" : "property",
                    area,
                    $"polson://sdk/core/{area}",
                    Signature(member, jsName),
                    member.DeclaringType?.Name ?? receiver.Types[0].Name,
                    member.Name,
                    !receiver.Types.Contains(member.DeclaringType)));
            }
        }

        return symbols
            .GroupBy(s => s.Name, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The globals a live engine actually registers, minus the ECMAScript built-ins.
    /// </summary>
    /// <remarks>
    /// Read from a running sandbox rather than from a list kept alongside the registration code,
    /// because a second list is a second thing to forget to update.
    /// </remarks>
    public static IReadOnlyList<string> RuntimeGlobals()
    {
        var engine = new JsDrawingEngine();
        var baseline = engine.Execute("Object.getOwnPropertyNames(globalThis).join(',')");
        var all = (baseline.ReturnValue?.ToString() ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries);

        // Everything the engine adds comes after `globalThis` in property-creation order.
        var pivot = Array.IndexOf(all, "globalThis");
        return pivot < 0 ? all : all[(pivot + 1)..];
    }

    public static string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("schema", "polson://sdk/symbols");
            w.WriteNumber("count", Symbols.Count);

            w.WriteStartArray("symbols");
            foreach (var s in Symbols)
            {
                w.WriteStartObject();
                w.WriteString("name", s.Name);
                w.WriteString("receiver", s.Receiver);
                w.WriteString("member", s.Member);
                w.WriteString("kind", s.Kind);
                w.WriteString("area", s.Area);
                w.WriteString("uri", s.Uri);
                w.WriteString("signature", s.Signature);
                w.WriteString("clrType", s.ClrType);
                w.WriteString("clrMember", s.ClrMember);
                w.WriteBoolean("inherited", s.Inherited);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Whether a query is a claim about the API rather than a description of a task.
    /// </summary>
    /// <remarks>
    /// A dotted, space-free name — <c>paper.squircle</c>, <c>Skia.Shader.sksl</c> — asserts that a
    /// call exists. That claim can be answered definitively, and it should be, because prose
    /// retrieval answers it by returning its nearest neighbour and thereby confirms calls that do
    /// not exist.
    /// </remarks>
    public static bool LooksLikeSymbol(string query) =>
        DottedName().IsMatch(Normalise(query));

    /// <summary>
    /// Resolves a query to the calls it names, tolerating the ways an agent writes one:
    /// backticks, a trailing <c>()</c>, or a bare member name with no receiver.
    /// </summary>
    public static IReadOnlyList<JsSymbol> ResolveQuery(string query)
    {
        var q = Normalise(query);
        if (q.Length == 0)
        {
            return [];
        }

        var exact = Resolve(q);
        if (exact is not null)
        {
            return [exact];
        }

        // A bare member name is ambiguous by design: drawCastShadow lives on both ctx and Drawing,
        // and an agent that asked for it wants to be shown both rather than made to guess.
        return q.Contains('.', StringComparison.Ordinal) ? [] : ByMember(q);
    }

    /// <summary>Every receiver carrying a member of this name.</summary>
    public static IReadOnlyList<JsSymbol> ByMember(string member) => Symbols
        .Where(s => string.Equals(s.Member, member, StringComparison.OrdinalIgnoreCase))
        .OrderBy(s => s.Inherited)
        .ThenBy(s => s.Name, StringComparer.Ordinal)
        .ToList();

    /// <summary>Strips the punctuation an agent tends to wrap a call name in.</summary>
    public static string Normalise(string query)
    {
        var q = (query ?? string.Empty).Trim().Trim('`', '\'', '"');
        var paren = q.IndexOf('(', StringComparison.Ordinal);
        if (paren > 0)
        {
            q = q[..paren];
        }

        return q.Trim();
    }

    /// <summary>Exact resolution. A null return is a real answer: no such call.</summary>
    public static JsSymbol? Resolve(string name) =>
        Symbols.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Closest names by edit distance, for the "did you mean" half of a negative answer.</summary>
    public static IReadOnlyList<JsSymbol> Nearest(string name, int k = 5) => Symbols
        .Select(s => (Symbol: s, Distance: Distance(s.Member, Member(name))))
        .OrderBy(x => x.Distance)
        .ThenBy(x => x.Symbol.Name, StringComparer.Ordinal)
        .Take(k)
        .Select(x => x.Symbol)
        .ToList();

    /// <summary>Everything callable on a receiver — an enumeration, not a search.</summary>
    public static IReadOnlyList<JsSymbol> OnReceiver(string receiver) => Symbols
        .Where(s => string.Equals(s.Receiver, receiver, StringComparison.OrdinalIgnoreCase))
        .ToList();

    /// <summary>The subject area whose reference slice actually documents this call, if any.</summary>
    static string? DocumentedArea(string name) => Slices
        .FirstOrDefault(s => s.Text.Contains(name, StringComparison.OrdinalIgnoreCase))
        .Area;

    /// <remarks>
    /// Resolved on first use rather than in a field initialiser: <see cref="Symbols"/> is declared
    /// above this and would otherwise read it while it is still null.
    /// </remarks>
    static (string Area, string Text)[] Slices => slices ??= BuildSlices();

    static (string Area, string Text)[] BuildSlices()
    {
        var core = PolsonResources.Docs.Core();
        return [.. PolsonResources.Docs.Addressable
            .Select(a => (a.Name, Text: SdkDocs.Slice(core, a.HeadingText)))
            .Where(x => x.Text is not null)
            .Select(x => (x.Name, x.Text!))];
    }

    static (string Area, string Text)[]? slices;

    /// <summary>A type and its bases, stopping short of <see cref="object"/>.</summary>
    /// <summary>
    /// A receiver's type and the bases a script can actually reach members through.
    /// </summary>
    /// <remarks>
    /// Stops at <see cref="Runtime"/> as well as at <c>object</c>. Several JS-reachable toolkits
    /// inherit <c>Runtime</c> for logging and configuration, and walking into it published about
    /// thirty .NET infrastructure members as though they were SDK calls —
    /// <c>Assets.downloadFile</c>, <c>Assets.entryAssembly</c>, even <c>Assets.camelDir</c>, a name
    /// left over from another project entirely.
    /// <para>
    /// That is worse than clutter. <c>Search</c> promises that a <c>direct</c> verdict means the call
    /// exists and its signature is authoritative, and for those members the promise was false — an
    /// agent asking about one would be told to go ahead and use it.
    /// </para>
    /// </remarks>
    static IEnumerable<Type> Ancestry(Type type)
    {
        for (var t = type; t is not null && t != typeof(object) && t != typeof(Runtime); t = t.BaseType)
        {
            yield return t;
        }
    }

    static string Member(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot < 0 ? name : name[(dot + 1)..];
    }

    /// <summary>The JS spelling of a .NET member name. Shared with the engine's error messages, so a
    /// suggested member is spelled the way the script must type it.</summary>
    internal static string JsName(string clr) => clr.Length == 0
        ? clr
        : char.ToLowerInvariant(clr[0]) + clr[1..];

    /// <summary>The member's signature, spelled the way the symbol is named.</summary>
    /// <param name="jsName">
    /// The resolved JS spelling. Passed in rather than recomputed, so a namespace accessor's
    /// signature reads <c>Shader: SkiaShaderApi</c> and not <c>shader: SkiaShaderApi</c> under a
    /// symbol called <c>Skia.Shader</c> — a search result that contradicts its own heading.
    /// </param>
    static string Signature(MemberInfo member, string jsName)
    {
        if (member is PropertyInfo p)
        {
            return $"{jsName}: {JsType(p.PropertyType)}";
        }

        var m = (MethodInfo)member;
        var args = m.GetParameters().Select(a =>
            $"{a.Name}{(a.IsOptional ? "?" : "")}: {JsType(a.ParameterType)}");

        return $"{jsName}({string.Join(", ", args)}) -> {JsType(m.ReturnType)}";
    }

    /// <summary>The JavaScript spelling of a CLR type, for text an agent reads.</summary>
    /// <remarks>
    /// Generics used to fall through to <c>t.Name</c>, which carries the arity suffix, so the symbol
    /// index advertised <c>Chart.createColumnChart(...) -> Dictionary`2</c>. `CLAUDE.md` §8 requires
    /// agent-facing text to use the JS spelling, and this is the reader's only description of what a
    /// call hands back — a CLR name there is a wrong answer, not an untidy one.
    /// </remarks>
    static string JsType(Type t)
    {
        var u = Nullable.GetUnderlyingType(t);
        if (u is not null) return JsType(u) + "?";
        if (t.IsArray) return JsType(t.GetElementType()!) + "[]";

        if (t.IsGenericType)
        {
            var definition = t.GetGenericTypeDefinition().Name;
            var args = t.GetGenericArguments();

            // The three Assets calls are the only async surface, and a script must await them.
            if (definition is "Task`1" or "ValueTask`1") return $"Promise<{JsType(args[0])}>";

            // A dictionary crosses the boundary as a plain object, which is what a script sees.
            if (definition.Contains("Dictionary", StringComparison.Ordinal)) return "object";

            // A callback parameter reads as a function rather than as its delegate type.
            if (definition.StartsWith("Func`", StringComparison.Ordinal)
                || definition.StartsWith("Action`", StringComparison.Ordinal)) return "function";

            // Every remaining single-argument shape here is a sequence.
            return args.Length == 1 ? JsType(args[0]) + "[]" : "object";
        }

        return t.Name switch
        {
            "Int32" or "Int64" or "Double" or "Single" or "Decimal" or "Byte" => "number",
            "String" => "string",
            "Boolean" => "boolean",
            "Void" => "void",
            "Object" => "any",
            "Task" or "ValueTask" => "Promise<void>",
            "JsonObject" or "JsonNode" => "object",
            "JsonArray" => "any[]",
            "Action" => "function",
            _ => t.Name,
        };
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)+$")]
    private static partial Regex DottedName();

    static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }
    #endregion
}
