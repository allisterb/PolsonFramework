namespace Polson.MCPServer;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        return reader.ReadToEnd();
    }

    public static string Index(SdkDocSet set) =>
        Memo($"{set.Label}/index", () => SdkDocs.BuildIndex(set));

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
                    () => Memo($"{set.Label}/{kind}/{area.Name}", () => SdkDocs.Slice(doc, area.HeadingText)!),
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
    public static string SdkIndex() => Index(Docs);

    [McpServerResource(UriTemplate = "polson://sdk/core", Name = "polson-sdk-core",
        Title = "Polson JS SDK Reference — Core Map", MimeType = "text/plain")]
    [Description("The SDK map — identical to 'polson-sdk-index' (polson://sdk/index). The per-area method reference " +
        "lives at polson://sdk/core/{Area}; polson://sdk/core/all serves the whole core document.")]
    public static string SdkCore() => Index(Docs);

    [McpServerResource(UriTemplate = "polson://sdk/core/all", Name = "polson-sdk-core-all",
        Title = "Polson JS SDK Reference — Core (Full Document)", MimeType = "text/plain")]
    [Description("The ENTIRE core method reference in one read. Prefer the map (polson://sdk/index) plus per-area resources.")]
    public static string SdkCoreAll() => Docs.Core();

    [McpServerResource(UriTemplate = "polson://sdk/schema", Name = "polson-sdk-schema",
        Title = "Polson JS SDK Reference — Schemas (Index)", MimeType = "text/plain")]
    [Description("Where model schemas live: served per subject area at polson://sdk/schema/{Area}. " +
        "polson://sdk/schema/all serves the whole document.")]
    public static string SdkSchema() => SchemaSignpost(Docs);

    [McpServerResource(UriTemplate = "polson://sdk/schema/all", Name = "polson-sdk-schema-all",
        Title = "Polson JS SDK Reference — Schemas (Full Document)", MimeType = "text/plain")]
    [Description("The ENTIRE schema reference in one read — every parameter, model, and return type of the Polson SDK.")]
    public static string SdkSchemaAll() => Docs.Schema();

    private static readonly ConcurrentDictionary<string, string> memo = new();
    private static string Memo(string key, Func<string> build) => memo.GetOrAdd(key, _ => build());
    #endregion
}

