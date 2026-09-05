namespace Polson.MCPServer;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

using ModelContextProtocol.Server;

public class PolsonResources
{
    #region Properties
    public static SdkDocSet Docs { get; } = new(
        Label: "Graphics & Drawing",
        Core: () => ReadEmbedded("Polson.core.md"),
        Schema: () => ReadEmbedded("Polson.schema.md"),
        Areas: SdkDocs.PolsonAreas,
        Extras: [],
        CorePreamble: ["Execution model", "Global Functions"],
        SchemaPreamble: ["Execution Envelopes"]);
    #endregion

    #region Methods
    public static string ReadEmbedded(string name)
    {
        var assembly = typeof(PolsonResources).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded SDK doc '{name}' was not found in assembly {assembly.FullName}. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Failed to open stream for '{resourceName}'");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        if (name.Contains("Polson.core.md", StringComparison.OrdinalIgnoreCase))
        {
            content = content.Replace("{{SCRIPT_TIMEOUT_SECONDS}}", JsDrawingEngine.ScriptTimeoutSeconds.ToString());
        }

        return content;
    }

    public static string Index(SdkDocSet set) =>
        Memo($"{set.Label}/index/{JsDrawingEngine.ScriptTimeoutSeconds}", () => SdkDocs.BuildIndex(set));

    public static string SchemaSignpost(SdkDocSet set) =>
        Memo($"{set.Label}/schema-signpost", () => SdkDocs.BuildSchemaSignpost(set));

    public static IEnumerable<McpServerResource> AreaResources(SdkDocSet set)
    {
        var documents = new[] { ("core", set.Core()), ("schema", set.Schema()) };
        foreach (var area in set.Addressable)
        {
            foreach (var (kind, doc) in documents)
            {
                var slice = SdkDocs.Slice(doc, area.HeadingText);
                if (slice is null) continue;

                var uri = $"polson://sdk/{kind}/{area.Name}";
                var name = $"polson-sdk-{kind}-{area.Name.ToLowerInvariant()}";
                var description = kind == "core"
                    ? $"{area.Name} — method reference: parameters, semantics, and examples for {area.Name}."
                    : $"{area.Name} — JSON schemas and models returned by {area.Name} methods.";

                yield return McpServerResource.Create(
                    () => Served(uri, Memo($"{set.Label}/{kind}/{area.Name}", () => SdkDocs.Slice(doc, area.HeadingText)!)),
                    new McpServerResourceCreateOptions
                    {
                        UriTemplate = uri,
                        Name = name,
                        Title = $"Polson JS SDK — {area.Name} ({kind})",
                        Description = description,
                        MimeType = "text/plain"
                    });
            }
        }
    }

    [McpServerResource(UriTemplate = "polson://sdk/index", Name = "polson-sdk-index",
        Title = "Polson JS SDK Reference — Map (Start Here)", MimeType = "text/plain")]
    [Description("START HERE for the Polson JavaScript SDK. The reference map: language support (ECMAScript 2025), " +
        "execution constraints, global functions, and the complete inventory of callable objects, factories, and models " +
        "grouped by subject area (polson://sdk/core/{Area} for methods, polson://sdk/schema/{Area} for schemas).")]
    public static string SdkIndex() => Served("polson://sdk/index", Index(Docs));

    [McpServerResource(UriTemplate = "polson://sdk/core", Name = "polson-sdk-core",
        Title = "Polson JS SDK Reference — Core Map", MimeType = "text/plain")]
    [Description("The SDK map — identical to 'polson-sdk-index' (polson://sdk/index). The per-area method reference " +
        "lives at polson://sdk/core/{Area}; polson://sdk/core/all serves the whole core document.")]
    public static string SdkCore() => Served("polson://sdk/core", Index(Docs));

    [McpServerResource(UriTemplate = "polson://sdk/core/all", Name = "polson-sdk-core-all",
        Title = "Polson JS SDK Reference — Core (Full Document)", MimeType = "text/plain")]
    [Description("The ENTIRE core method reference in one read. Prefer the map (polson://sdk/index) plus per-area resources.")]
    public static string SdkCoreAll() => Served("polson://sdk/core/all", Docs.Core());

    [McpServerResource(UriTemplate = "polson://sdk/schema", Name = "polson-sdk-schema",
        Title = "Polson JS SDK Reference — Schemas (Index)", MimeType = "text/plain")]
    [Description("Where model schemas live: served per subject area at polson://sdk/schema/{Area}. " +
        "polson://sdk/schema/all serves the whole document.")]
    public static string SdkSchema() => Served("polson://sdk/schema", SchemaSignpost(Docs));

    [McpServerResource(UriTemplate = "polson://sdk/schema/all", Name = "polson-sdk-schema-all",
        Title = "Polson JS SDK Reference — Schemas (Full Document)", MimeType = "text/plain")]
    [Description("The ENTIRE schema reference in one read — every parameter, model, and return type of the Polson SDK.")]
    public static string SdkSchemaAll() => Served("polson://sdk/schema/all", Docs.Schema());

    [McpServerResource(UriTemplate = "polson://sdk/symbols", Name = "polson-sdk-symbols",
        Title = "Polson JS SDK — Symbol Index (Machine-Readable)", MimeType = "application/json")]
    [Description("Every callable name on the JavaScript surface, generated by reflection over the types the engine " +
        "actually registers — so it cannot drift from the code. Each entry carries its signature, subject area, and " +
        "the resource URI documenting it. Use this to confirm a call exists and how it is spelled BEFORE writing a " +
        "script; unlike Search, an absent name here means the call genuinely does not exist.")]
    public static string SdkSymbols() => Served("polson://sdk/symbols", Memo("symbols", JsSymbolManifest.ToJson));

    [McpServerResource(UriTemplate = "polson://sdk/symbols/{Receiver}", Name = "polson-sdk-symbols-receiver",
        Title = "Polson JS SDK — Symbols On One Receiver", MimeType = "text/plain")]
    [Description("Everything callable on one receiver, listed rather than searched — e.g. 'paper', 'ctx', 'element', " +
        "'Drawing', 'Skia.Shader'. Answers 'what can I call on this?' completely, which a similarity search cannot.")]
    public static string SdkSymbolsForReceiver(string receiver) => Served($"polson://sdk/symbols/{receiver}", Memo($"symbols/{receiver}", () =>
    {
        var symbols = JsSymbolManifest.OnReceiver(receiver);
        if (symbols.Count == 0)
        {
            var known = string.Join(", ", JsSurface.Receivers.Select(r => r.Name));
            return $"No receiver named '{receiver}'. Known receivers: {known}";
        }

        var sb = new StringBuilder()
            .AppendLine($"# {receiver} — {symbols.Count} members")
            .AppendLine();

        foreach (var group in symbols.GroupBy(s => s.Inherited).OrderBy(g => g.Key))
        {
            if (group.Key)
            {
                sb.AppendLine().AppendLine("## Inherited (documented on the base receiver)").AppendLine();
            }

            foreach (var s in group.OrderBy(s => s.Member, StringComparer.Ordinal))
            {
                sb.AppendLine($"- `{receiver}.{s.Signature}`");
            }
        }

        return sb.AppendLine().AppendLine($"Full reference: {symbols[0].Uri}").ToString();
    }));

    /// <summary>
    /// Called with every document URI served, however it was asked for — as an MCP resource or
    /// through <c>ReadDoc</c>. Set by the server at registration so the static resource methods can
    /// reach the run's event log.
    /// </summary>
    /// <remarks>
    /// **The run record had no way to say which documentation an agent read**, and that absence cost
    /// a whole session of forensics: answering "did it read the Chart reference?" meant reconstructing
    /// it from Serilog text files that die with the container, and from an SSE transcript that
    /// happened to be captured. Eighteen event types across every run ever recorded, and not one of
    /// them was a resource read.
    ///
    /// It matters more here than an ordinary log line would, because on ADK a resource is injected
    /// for a single turn and never persisted — so *how often* a document was fetched is the
    /// difference between an agent that has it and one that had it once.
    /// </remarks>
    public static Action<string>? OnRead { get; set; }

    /// <summary>Records a served document, ignoring any failure — instrumentation must not break a read.</summary>
    internal static string Served(string uri, string body)
    {
        try { OnRead?.Invoke(uri); } catch { /* a broken log must not fail the read */ }
        return body;
    }

    /// <summary>
    /// Resolves a <c>polson://</c> URI to the document body it serves, or null when nothing is
    /// published at it. <see cref="KnownUris"/> lists what is.
    /// </summary>
    /// <remarks>
    /// The same documents are also published as MCP <em>resources</em>, and this exists because a
    /// resource is not reachable the way a tool is. A live run showed the cost: the host's
    /// <c>load_mcp_resource</c> returns only a status line ("temporarily inserted and removed"),
    /// injects the body into the <em>next</em> model request, and never persists it — so the agent
    /// tried it once, could not tell whether it had worked, and fell back to <c>Search</c> for the
    /// rest of the run. It then knew <c>Chart.createWaffle</c> existed and not how to call it, and
    /// hand-rolled the chart. Serving the identical text through a tool costs one method and
    /// removes that whole failure mode.
    /// </remarks>
    public static string? Read(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var trimmed = uri.Trim();
        // Bare 'manual/13', '13' and 'sdk/core/Chart' are what an agent types when it is copying a
        // reference out of prose rather than a URI out of a result, so accept them too.
        var path = trimmed.StartsWith("polson://", StringComparison.OrdinalIgnoreCase)
            ? trimmed["polson://".Length..]
            : trimmed;
        path = path.Trim('/');

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        if (segments[0].Equals("manual", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length == 1) return PolsonManuals.ManualIndex();
            if (segments[1].Equals("index", StringComparison.OrdinalIgnoreCase)) return PolsonManuals.ManualIndex();
            var manual = PolsonManuals.Find(segments[1]);
            return manual is null ? null : PolsonManuals.ServedBody(manual);
        }

        // A lone segment that is not a scheme word is a manual id or slug: '13' is what an agent
        // types when it has read "see manual 13" in prose rather than copied a URI out of a result.
        if (segments.Length == 1 && !segments[0].Equals("sdk", StringComparison.OrdinalIgnoreCase))
        {
            return PolsonManuals.Find(segments[0]) is { } bare ? PolsonManuals.ServedBody(bare) : null;
        }

        if (!segments[0].Equals("sdk", StringComparison.OrdinalIgnoreCase)) return null;
        if (segments.Length == 1) return Index(Docs);

        var kind = segments[1];
        var leaf = segments.Length > 2 ? segments[2] : null;

        if (kind.Equals("index", StringComparison.OrdinalIgnoreCase)) return Index(Docs);

        if (kind.Equals("symbols", StringComparison.OrdinalIgnoreCase))
        {
            return leaf is null ? SdkSymbols() : SdkSymbolsForReceiver(leaf);
        }

        var core = kind.Equals("core", StringComparison.OrdinalIgnoreCase);
        if (!core && !kind.Equals("schema", StringComparison.OrdinalIgnoreCase)) return null;

        if (leaf is null) return core ? Index(Docs) : SchemaSignpost(Docs);
        if (leaf.Equals("all", StringComparison.OrdinalIgnoreCase)) return core ? Docs.Core() : Docs.Schema();

        var area = Docs.Addressable.FirstOrDefault(a => a.Name.Equals(leaf, StringComparison.OrdinalIgnoreCase));
        if (area is null) return null;

        var document = core ? Docs.Core() : Docs.Schema();
        return Memo($"{Docs.Label}/{(core ? "core" : "schema")}/{area.Name}",
            () => SdkDocs.Slice(document, area.HeadingText) ?? string.Empty) is { Length: > 0 } body
            ? body
            : null;
    }

    /// <summary>Every URI <see cref="Read"/> serves, for naming them when a lookup misses.</summary>
    public static IReadOnlyList<string> KnownUris()
    {
        var uris = new List<string>
        {
            "polson://sdk/index", "polson://sdk/core/all", "polson://sdk/schema/all",
            "polson://sdk/symbols", "polson://manual/index"
        };

        foreach (var area in Docs.Addressable)
        {
            if (SdkDocs.Slice(Docs.Core(), area.HeadingText) is not null) uris.Add($"polson://sdk/core/{area.Name}");
            if (SdkDocs.Slice(Docs.Schema(), area.HeadingText) is not null) uris.Add($"polson://sdk/schema/{area.Name}");
        }

        foreach (var manual in PolsonManuals.All) uris.Add($"polson://manual/{manual.Id}");
        return uris;
    }

    private static readonly ConcurrentDictionary<string, string> memo = new();
    private static string Memo(string key, Func<string> build) => memo.GetOrAdd(key, _ => build());
    #endregion
}

