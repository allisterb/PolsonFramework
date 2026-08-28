namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public sealed record SdkArea(string Name, string[]? SharedGlobals = null, string? Heading = null)
{
    #region Properties
    public string[] Globals => SharedGlobals ?? [Name];
    public string HeadingText => Heading ?? Name;
    #endregion
}

public sealed record SdkDocSet(
    string Label,
    Func<string> Core,
    Func<string> Schema,
    SdkArea[] Areas,
    SdkArea[] Extras,
    string[] CorePreamble,
    string[] SchemaPreamble)
{
    #region Properties
    public IEnumerable<SdkArea> Addressable => Areas.Concat(Extras);
    #endregion
}

public static class SdkDocs
{
    #region Properties
    public static SdkArea[] PolsonAreas { get; } =
    [
        new("Globals", ["console", "log", "error", "exit", "table", "mina"], "Global Functions"),
        new("Snap", ["Snap", "mina"], "Snap"),
        new("Canvas2D", ["createCanvas", "Canvas"], "Canvas2D"),
        new("Skia", ["Skia", "SK", "ImageData"], "Skia"),
        new("Drawing", ["Drawing"], "Drawing"),
        new("Logo", ["Logo"], "Logo"),
        new("VectorLogo", ["VectorLogo", "vectorLogo"], "VectorLogo & Snap.svg Logo Methods"),
        new("LogoType", ["LogoType", "logoType", "Typography"], "LogoType"),
        new("Assets", ["Assets", "ExtendedMind"], "Assets (Cloud Asset Requisition)")
    ];
    #endregion

    #region Methods
    public static string? Slice(string doc, string area)
    {
        if (string.IsNullOrEmpty(doc) || string.IsNullOrEmpty(area)) return null;

        var headings = Headings(doc);
        var lines = doc.Split('\n');
        var slices = headings
            .Select((h, i) => (Heading: h, Next: headings.Skip(i + 1).FirstOrDefault(n => n.Level <= h.Level)))
            .Where(s => Names(s.Heading.Text, area))
            .Select(s => string.Join('\n', lines[s.Heading.Line..(s.Next.Line > 0 ? s.Next.Line : lines.Length)]).TrimEnd())
            .ToArray();

        return slices.Length == 0 ? null : string.Join("\n\n", slices);
    }

    public static List<(int Line, int Level, string Text)> Headings(string doc)
    {
        var headings = new List<(int, int, string)>();
        var fenced = false;
        var lines = doc.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.StartsWith("```", StringComparison.Ordinal)) { fenced = !fenced; continue; }
            if (fenced || !line.StartsWith('#')) continue;
            var level = line.TakeWhile(c => c == '#').Count();
            if (level > 6 || level >= line.Length || line[level] != ' ') continue;
            headings.Add((i, level, line[(level + 1)..].Trim()));
        }
        return headings;
    }

    public static string BuildIndex(SdkDocSet set)
    {
        var core = set.Core();
        var schema = set.Schema();
        var index = new StringBuilder();

        index.AppendLine($"# Polson JavaScript SDK — Reference Map ({set.Label})");
        index.AppendLine();
        index.AppendLine("""
            **This map replaces reading the reference documents whole.** It carries the execution model and the
            language support sections in full, followed by the complete inventory of callable objects, factory functions,
            and returned models grouped by subject area:

            - `polson://sdk/core/{Area}` — methods, parameters, semantics, and usage for that area.
            - `polson://sdk/schema/{Area}` — JSON schemas and fields for models that area's methods return.
            - `polson://sdk/core/all`, `polson://sdk/schema/all` — whole documents.

            Read the areas your task touches, then write your script.
            """);
        index.AppendLine();

        foreach (var section in set.CorePreamble)
            Append(index, Slice(core, section));
        foreach (var section in set.SchemaPreamble)
            Append(index, Slice(schema, section));

        index.AppendLine();
        index.AppendLine("# Areas — The Inventory");
        index.AppendLine();
        foreach (var area in set.Areas)
        {
            var coreSlice = Slice(core, area.HeadingText);
            var schemaSlice = Slice(schema, area.HeadingText);
            var methods = MethodNames(coreSlice ?? "", area.Name);
            var models = ModelNames(schemaSlice ?? "");
            index.AppendLine($"## {area.Name}");
            index.AppendLine();
            if (Purpose(coreSlice) is string purpose) index.AppendLine(purpose);
            if (area.SharedGlobals is not null)
                index.AppendLine($"Globals: `{string.Join("`, `", area.Globals)}`");
            index.AppendLine();
            index.AppendLine($"- Methods ({methods.Count}) — detail in `polson://sdk/core/{area.Name}`:");
            index.AppendLine($"  {(methods.Count > 0 ? string.Join(", ", methods) : "see core reference")}");
            index.AppendLine($"- Models ({models.Count}) — schemas in `polson://sdk/schema/{area.Name}`:");
            index.AppendLine($"  {(models.Count > 0 ? string.Join(", ", models) : "none")}");
            index.AppendLine();
        }

        if (set.Extras.Length > 0)
        {
            index.AppendLine("## Other Sections");
            index.AppendLine();
            foreach (var extra in set.Extras)
            {
                var kind = Slice(core, extra.HeadingText) is not null ? "core" : "schema";
                index.AppendLine($"- **{extra.HeadingText}** → `polson://sdk/{kind}/{extra.Name}`");
            }
            index.AppendLine();
        }
        return index.ToString();
    }

    public static string BuildSchemaSignpost(SdkDocSet set)
    {
        var areas = string.Join(", ", set.Areas.Select(a => a.Name));
        return $$"""
            # Polson JavaScript SDK — Schemas ({{set.Label}})

            Model schemas are served **per subject area**:

            - `polson://sdk/schema/{Area}` — exact fields of every model that area's methods return.
            - `polson://sdk/index` — reference map and method inventory.
            - `polson://sdk/schema/all` — whole schema document.

            Areas: {{areas}}.
            """;
    }

    public static List<string> MethodNames(string slice, string? area = null)
    {
        var names = new List<string>();
        foreach (var line in slice.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("- `") || trimmed.StartsWith("### `"))
            {
                var match = Regex.Match(trimmed, @"`([A-Za-z0-9_.]+\([^\)]*\))`");
                if (match.Success)
                {
                    var sig = match.Groups[1].Value;
                    if (!names.Contains(sig, StringComparer.Ordinal)) names.Add(sig);
                }
            }
        }
        return names;
    }

    public static List<string> ModelNames(string slice)
    {
        var names = new List<string>();
        foreach (var line in slice.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("## `") && trimmed.EndsWith('`'))
            {
                var name = trimmed[4..^1].Trim();
                if (!string.IsNullOrEmpty(name) && !names.Contains(name, StringComparer.Ordinal))
                    names.Add(name);
            }
        }
        return names;
    }

    private static bool Names(string headingText, string area)
    {
        var h = headingText.Trim();
        var a = area.Trim();
        if (string.Equals(h, a, StringComparison.OrdinalIgnoreCase)) return true;

        var paren = h.IndexOf('(');
        var baseHeading = paren > 0 ? h[..paren].Trim() : h;
        if (string.Equals(baseHeading, a, StringComparison.OrdinalIgnoreCase)) return true;

        var parts = baseHeading.Split(['/', '—', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim());
        return parts.Any(p => string.Equals(p, a, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Purpose(string? slice)
    {
        if (string.IsNullOrEmpty(slice)) return null;
        var lines = slice.Split('\n');
        foreach (var raw in lines.Skip(1))
        {
            var line = raw.Trim();
            if (line.Length > 0 && !line.StartsWith('#') && !line.StartsWith('-') && !line.StartsWith('`'))
                return line;
        }
        return null;
    }

    private static void Append(StringBuilder sb, string? slice)
    {
        if (string.IsNullOrEmpty(slice)) return;
        sb.AppendLine(slice);
        sb.AppendLine();
    }
    #endregion
}

