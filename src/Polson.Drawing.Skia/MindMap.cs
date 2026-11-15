namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>Where a mind map's node came from: an association, a measurement, or an assumption said as one.</summary>
public enum MindMapKind
{
    /// <summary>An ordinary association.</summary>
    Plain,

    /// <summary>A title or tag a Qloo answer returned, cited by its handle: <c>*Q-Tip* (t4)</c>.</summary>
    Measured,

    /// <summary>The agent's own cultural knowledge or translation, marked <c>(assumed)</c>: not measured, not checked.</summary>
    Assumed,
}

/// <summary>One node of a mind map, parsed from an indented Markdown outline.</summary>
public sealed class MindMapNode
{
    #region Properties
    /// <summary>The text as drawn: the outline's words without their Markdown or their provenance.</summary>
    public string Text { get; init; } = string.Empty;

    public MindMapKind Kind { get; init; }

    /// <summary>For a measured node, the Taste handle it cites (<c>t4</c>).</summary>
    public string? Handle { get; init; }

    /// <summary>For a measured node, true when written as a quoted tag, false as an italic title, null when neither.</summary>
    public bool? IsTag { get; init; }

    /// <summary>Set by the caller once a measured node has been looked for in its answer.</summary>
    public bool? Verified { get; set; }

    public int Depth { get; init; }

    public MindMapNode? Parent { get; init; }

    public List<MindMapNode> Children { get; } = [];

    /// <summary>The path from the centre's branch to this node: <c>Synonyms → cool → composed</c>.</summary>
    public string Trail => Parent is null || Parent.Parent is null ? Text : $"{Parent.Trail} → {Text}";

    /// <summary>The top-level branch this node grows from, or itself for a branch; null for the centre.</summary>
    public MindMapNode? Branch => Parent is null ? null : Parent.Parent is null ? this : Parent.Branch;

    public IEnumerable<MindMapNode> Descendants => Children.SelectMany(c => c.Descendants.Prepend(c));

    public IEnumerable<MindMapNode> Leaves => Children.Count == 0 ? [this] : Children.SelectMany(c => c.Leaves);

    internal float Angle { get; set; }
    internal float X { get; set; }
    internal float Y { get; set; }
    #endregion
}

/// <summary>
/// Lupton's mind map (<i>Graphic Design Thinking</i>, pp. 22–25, after Tony Buzan): one term at the centre, a web of
/// associations branching from it, a colour per branch. Parsed from the outline an agent writes, and drawn radially.
/// </summary>
/// <remarks>
/// <para>
/// <b>The outline is the thinking; the drawing is for the reader.</b> Writing words is fast and needs no layout, which
/// is what Lupton asks of the exercise, so the agent writes <c>mindmap.md</c> — a <c>#</c> heading for the centre and
/// nested <c>-</c> bullets for the branches — and this lays it out.
/// </para>
/// <para>
/// <b>Leaves sit on the outer ring and their labels run outward along the radius</b> (a radial dendrogram), so no two
/// labels can collide: neighbouring leaves are an angle apart, and the ring is made large enough that the angle is at
/// least a line of type. Branches and their sub-branches sit on inner rings, at the middle of the leaves they hold.
/// </para>
/// </remarks>
public static partial class MindMap
{
    #region Properties
    /// <summary>A colour per branch, as Buzan asks: distinct in hue and in lightness, each legible under white type.</summary>
    public static readonly string[] Palette =
        ["#c8553d", "#2a7f8f", "#d4a017", "#6a4c93", "#4f8a3c", "#b5446e", "#3d5a80", "#9a6324", "#1b998b", "#7d5ba6"];

    /// <summary>The widest a branch's name is drawn, in px.</summary>
    private const float BranchPill = 240;
    #endregion

    #region Methods
    /// <summary>
    /// Parses an outline: one <c># centre</c> heading, then <c>-</c> or <c>*</c> bullets nested by indentation. A node
    /// ending <c>(t4)</c> is measured, cited to that Taste answer; one ending <c>(assumed)</c> is an assumption.
    /// </summary>
    public static MindMapNode Parse(string markdown)
    {
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        var heading = lines.Select(l => Heading().Match(l)).FirstOrDefault(m => m.Success)
            ?? throw new ArgumentException("Stage.mindMap: the outline needs a '# ' heading for the centre, e.g. '# Be Cool — the music business'.");
        var root = new MindMapNode { Text = Clean(heading.Groups[1].Value).Text, Depth = 0 };

        // Indentation is read relative to what came before, so two spaces, four or a tab all nest the same way.
        var stack = new List<(int Indent, MindMapNode Node)> { (-1, root) };
        foreach (var line in lines)
        {
            if (Bullet().Match(line) is not { Success: true } m) continue;
            var indent = m.Groups[1].Value.Replace("\t", "    ").Length;
            while (stack.Count > 1 && stack[^1].Indent >= indent) stack.RemoveAt(stack.Count - 1);
            var parent = stack[^1].Node;
            var (text, kind, handle, tag) = Clean(m.Groups[2].Value);
            if (text.Length == 0) continue;
            var node = new MindMapNode { Text = text, Kind = kind, Handle = handle, IsTag = tag, Depth = parent.Depth + 1, Parent = parent };
            parent.Children.Add(node);
            stack.Add((indent, node));
        }

        if (root.Children.Count == 0)
            throw new ArgumentException("Stage.mindMap: the outline has a centre and no branches. Write them as '- ' bullets under the heading, nested by indentation.");
        return root;
    }

    /// <summary>
    /// Draws the map into the box at (<paramref name="x"/>, <paramref name="y"/>), its leaves' type as large as the box
    /// allows up to <paramref name="maxType"/> px. Returns the leaf type size used.
    /// </summary>
    public static float Draw(CanvasRenderingContext2D ctx, MindMapNode root, float x, float y, float width, float height,
        string family = "Georgia", float maxType = 14, bool legend = true)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(root);
        var leaves = root.Leaves.ToArray();
        var branches = root.Children;
        var depth = leaves.Max(l => l.Depth);
        var legendHeight = legend ? 26f : 0f;
        float cx = x + width / 2, cy = y + (height - legendHeight) / 2;
        float halfW = width / 2 - 8, halfH = (height - legendHeight) / 2 - 8, half = MathF.Min(halfW, halfH);

        // Each leaf gets one slot of angle, and each gap between branches half a slot, so branches read as groups.
        var slot = 2 * MathF.PI / (leaves.Length + 0.5f * branches.Count);
        var angle = -MathF.PI / 2;
        foreach (var branch in branches)
        {
            foreach (var leaf in branch.Leaves) { leaf.Angle = angle + slot / 2; angle += slot; }
            angle += slot / 2;
        }
        Place(root);

        // The ring must hold a line of type per slot. Past that it is as large as leaves room for most labels: a long
        // label is cut to fit rather than let it shrink the whole map (the first draft's longest stock phrase pulled
        // the ring in until every branch name was cut instead). The full words stay in the outline.
        float size = maxType;
        while (size > 9 && size * 1.4f / slot > half * 0.72f) size -= 0.5f;
        ctx.Font = $"{size}px {family}";
        var widths = leaves.Select(l => Width(ctx, l.Text)).OrderBy(w => w).ToArray();
        var typical = widths[(int)(widths.Length * 0.85f) == widths.Length ? widths.Length - 1 : (int)(widths.Length * 0.85f)];
        var ring = Math.Clamp(half - typical - 14, MathF.Max(half * 0.5f, size * 1.4f / slot), half * 0.72f);

        // A label runs out along its own direction, so its room is the distance from the ring to the box's edge that
        // way: a leaf pointing sideways out of a wide box has far more than one pointing up.
        float Room(float a) => MathF.Min(halfW / MathF.Max(1e-3f, MathF.Abs(MathF.Cos(a))), halfH / MathF.Max(1e-3f, MathF.Abs(MathF.Sin(a)))) - ring - 12;

        // The centre wraps rather than cuts, and each branch stands clear of it in its own direction: beside a wide
        // centre a branch needs more room than above it, by the width of its own name.
        var centre = Wrap(ctx, root.Text, MathF.Min(ring * 0.8f, 230), size + 3, family);
        var clear = new Dictionary<MindMapNode, float>();
        foreach (var b in branches)
        {
            ctx.Font = $"bold {size}px {family}";
            var pw = MathF.Min(Width(ctx, b.Text), BranchPill - 16) + 16;
            float c = MathF.Abs(MathF.Cos(b.Angle)), s = MathF.Abs(MathF.Sin(b.Angle));
            var sideways = (centre.Width / 2 + pw / 2 + 10) / MathF.Max(1e-3f, c);
            var upright = (centre.Height / 2 + size + 16) / MathF.Max(1e-3f, s);
            clear[b] = MathF.Max(MathF.Min(sideways, upright), MathF.Max(centre.Width, centre.Height) / 2 + size);
        }
        var inner = ring * 0.8f;
        float Planned(MindMapNode n)
        {
            var start = MathF.Min(clear[n.Branch!], inner);
            return depth <= 2 ? start : start + (inner - start) * (n.Depth - 1) / (depth - 2);
        }

        // Inner pills are placed one at a time, centre outward, and each moves out along its own direction until its
        // box clears every box already placed: pills differ in width, so no fixed set of rings keeps a wide branch name
        // off the sub-branch beside it (a small map's "Films" sat on "Audience A — Berlin 30–34").
        // A pill must also stay inside the leaf ring, where the leaves' marks and labels begin: whole names pushed out
        // to the ring covered leaf labels (poster5's "Artists it loves that Mumbai does not" over "Zakir Hussain").
        // So a pill that cannot fit at full width is tried narrower — its name cut — rather than pushed over the leaves.
        var radius = new Dictionary<MindMapNode, float> { [root] = 0 };
        var pillWidths = new Dictionary<MindMapNode, float>();
        var boxes = new List<(float L, float T, float R, float B)> { (cx - centre.Width / 2, cy - centre.Height / 2, cx + centre.Width / 2, cy + centre.Height / 2) };
        bool Inside((float L, float T, float R, float B) b) =>
            new[] { (b.L, b.T), (b.R, b.T), (b.L, b.B), (b.R, b.B) }.All(p => MathF.Sqrt((p.Item1 - cx) * (p.Item1 - cx) + (p.Item2 - cy) * (p.Item2 - cy)) <= ring - 10);
        foreach (var n in root.Descendants.Where(n => n.Children.Count > 0).OrderBy(n => n.Depth))
        {
            var fontSize = n.Depth == 1 ? size : size - 1;
            (float L, float T, float R, float B) box = default;
            float r = 0, max = PillWidth(n, slot, Planned(n));
            foreach (var cap in new[] { max, max * 0.75f, max * 0.55f, 80f })
            {
                var (w, h) = PillSize(ctx, n.Text, cap, fontSize, family, n.Depth == 1);
                var found = false;
                for (r = MathF.Max(Planned(n), radius[n.Parent!] + 4); r < ring; r += 4)
                {
                    float px = cx + r * MathF.Cos(n.Angle), py = cy + r * MathF.Sin(n.Angle);
                    box = (px - w / 2 - 4, py - h / 2 - 4, px + w / 2 + 4, py + h / 2 + 4);
                    if (!Inside(box)) break;
                    if (!boxes.Any(o => box.L < o.R && o.L < box.R && box.T < o.B && o.T < box.B)) { found = true; break; }
                }
                pillWidths[n] = cap;
                if (found) break;
                r = MathF.Max(Planned(n), radius[n.Parent!] + 4);           // nothing fit: the narrowest, where it was planned
            }
            boxes.Add(box);
            radius[n] = r;
        }
        float Radius(MindMapNode n) => n.Children.Count == 0 ? ring : radius[n];
        foreach (var n in root.Descendants)
        {
            n.X = cx + Radius(n) * MathF.Cos(n.Angle);
            n.Y = cy + Radius(n) * MathF.Sin(n.Angle);
        }
        root.X = cx; root.Y = cy;

        // Edges first, thick near the centre and thinner outward, curving out along the child's own direction.
        ctx.Save();
        foreach (var n in root.Descendants)
        {
            var parent = n.Parent!;
            var bend = Radius(parent) + (Radius(n) - Radius(parent)) * 0.45f;
            ctx.StrokeStyle = Colour(n);
            ctx.LineWidth = MathF.Max(1f, 5.5f - 1.5f * (n.Depth - 1));
            ctx.SetLineDash(n.Kind == MindMapKind.Assumed ? new object[] { 4, 3 } : Array.Empty<object>());
            ctx.BeginPath();
            ctx.MoveTo(parent.X, parent.Y);
            ctx.QuadraticCurveTo(cx + bend * MathF.Cos(n.Angle), cy + bend * MathF.Sin(n.Angle), n.X, n.Y);
            ctx.Stroke();
        }
        ctx.SetLineDash(Array.Empty<object>());

        // Leaves: a mark for what kind of node it is, then the label running outward along the radius.
        foreach (var leaf in leaves)
        {
            Mark(ctx, leaf, leaf.X, leaf.Y);
            ctx.Save();
            ctx.Translate(leaf.X, leaf.Y);
            var left = MathF.Cos(leaf.Angle) < 0;
            ctx.Rotate(left ? leaf.Angle + MathF.PI : leaf.Angle);
            ctx.Font = $"{(leaf.Kind == MindMapKind.Assumed ? "italic " : "")}{size}px {family}";
            ctx.TextAlign = left ? "right" : "left";
            ctx.TextBaseline = "middle";
            ctx.FillStyle = leaf.Kind == MindMapKind.Assumed ? "#6b675e" : "#15151a";
            ctx.FillText(Fit(ctx, leaf.Text, Room(leaf.Angle)), left ? -8 : 8, 0);
            ctx.Restore();
        }

        // Inner nodes: a pill in the branch's colour, as wide as its share of the ring allows.
        foreach (var n in root.Descendants.Where(n => n.Children.Count > 0).OrderByDescending(n => n.Depth))
        {
            Pill(ctx, n.Text, n.X, n.Y, pillWidths[n], n.Depth == 1 ? size : size - 1, family, Colour(n), n.Depth == 1);
        }

        // The centre, in ink, on as many lines as it needs (at most three).
        ctx.Font = $"bold {size + 3}px {family}";
        ctx.FillStyle = "#15151a";
        ctx.BeginPath(); ctx.RoundRect(cx - centre.Width / 2, cy - centre.Height / 2, centre.Width, centre.Height, 14); ctx.Fill();
        ctx.FillStyle = "#ffffff"; ctx.TextAlign = "center"; ctx.TextBaseline = "middle";
        var lead = (size + 3) * 1.25f;
        for (var i = 0; i < centre.Lines.Length; i++)
            ctx.FillText(centre.Lines[i], cx, cy + (i - (centre.Lines.Length - 1) / 2f) * lead);

        if (legend) Legend(ctx, x + 4, y + height - 10, family, [.. root.Descendants.Select(n => (n.Kind, n.Verified))]);
        ctx.Restore();
        return size;
    }
    #endregion

    #region Private methods
    /// <summary>An inner node's angle is the middle of the leaves it holds.</summary>
    private static void Place(MindMapNode node)
    {
        foreach (var child in node.Children) Place(child);
        if (node.Children.Count > 0 && node.Parent is not null)
        {
            var leaves = node.Leaves.ToArray();
            node.Angle = (leaves[0].Angle + leaves[^1].Angle) / 2;
        }
    }

    private static string Colour(MindMapNode n) =>
        n.Branch is { } b ? Palette[b.Parent!.Children.IndexOf(b) % Palette.Length] : "#15151a";

    private static void Mark(CanvasRenderingContext2D ctx, MindMapNode n, float x, float y)
    {
        ctx.BeginPath();
        switch (n.Kind)
        {
            case MindMapKind.Measured when n.Verified == false:
                // Cited to an answer that did not return it: drawn as a fault, not quietly as evidence.
                ctx.Arc(x, y, 5, 0, MathF.Tau);
                ctx.StrokeStyle = "#c0262d"; ctx.LineWidth = 2; ctx.Stroke();
                break;
            case MindMapKind.Measured:
                ctx.Arc(x, y, 4.5f, 0, MathF.Tau);
                ctx.FillStyle = Colour(n); ctx.Fill();
                break;
            case MindMapKind.Assumed:
                ctx.Arc(x, y, 4, 0, MathF.Tau);
                ctx.FillStyle = "#ffffff"; ctx.Fill();
                ctx.StrokeStyle = Colour(n); ctx.LineWidth = 1.5f; ctx.Stroke();
                break;
            default:
                ctx.Arc(x, y, 2.5f, 0, MathF.Tau);
                ctx.FillStyle = Colour(n); ctx.Fill();
                break;
        }
    }

    /// <summary>
    /// Every inner node keeps its whole name, up to the cap: placement pushes a wide pill outward until it is clear, so
    /// a width capped by the node's share of the ring only cut names ("Key wo…", "Artists it lov…" in poster5).
    /// </summary>
    private static float PillWidth(MindMapNode n, float slot, float radius) => BranchPill;

    private static (float Width, float Height) PillSize(CanvasRenderingContext2D ctx, string text, float maxWidth, float size, string family, bool bold)
    {
        ctx.Font = $"{(bold ? "bold " : "")}{size}px {family}";
        var lines = PillLines(ctx, text, maxWidth - 16);
        return (lines.Max(l => Width(ctx, l)) + 16, PillHeight(lines.Length, size));
    }

    /// <summary>
    /// A pill's name on one line, or broken onto two between words when one line would be cut; cut only past that.
    /// lanternkeepermotion1 (2026-11-14) drew "Coastal unease" as "Coastal u…" in a pill that had room for two lines.
    /// </summary>
    private static string[] PillLines(CanvasRenderingContext2D ctx, string text, float width)
    {
        if (Width(ctx, text) <= width) return [text];
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // The break that leaves the longer line shortest, so the two lines balance.
        var split = Enumerable.Range(1, Math.Max(0, words.Length - 1))
            .Select(k => (First: string.Join(' ', words[..k]), Second: string.Join(' ', words[k..])))
            .OrderBy(p => MathF.Max(Width(ctx, p.First), Width(ctx, p.Second)))
            .FirstOrDefault();
        return split.First is null ? [Fit(ctx, text, width)] : [Fit(ctx, split.First, width), Fit(ctx, split.Second, width)];
    }

    private static float PillHeight(int lines, float size) => size + 10 + (lines - 1) * size * 1.15f;

    private static void Pill(CanvasRenderingContext2D ctx, string text, float x, float y, float maxWidth, float size, string family, string fill, bool bold)
    {
        ctx.Font = $"{(bold ? "bold " : "")}{size}px {family}";
        var lines = PillLines(ctx, text, maxWidth - 16);
        var w = lines.Max(l => Width(ctx, l)) + 16;
        var h = PillHeight(lines.Length, size);
        ctx.FillStyle = fill;
        ctx.BeginPath(); ctx.RoundRect(x - w / 2, y - h / 2, w, h, (size + 10) / 2); ctx.Fill();
        ctx.FillStyle = "#ffffff"; ctx.TextAlign = "center"; ctx.TextBaseline = "middle";
        for (var i = 0; i < lines.Length; i++)
            ctx.FillText(lines[i], x, y + 0.5f + (i - (lines.Length - 1) / 2f) * size * 1.15f);
    }

    /// <summary>
    /// A key to the marks the map uses, and only those: a map with no measured leaf — every map made without Qloo — would
    /// otherwise explain a mark it never draws, and name a service the work never used (lanternkeepermotion1).
    /// </summary>
    private static void Legend(CanvasRenderingContext2D ctx, float x, float y, string family, IReadOnlyList<(MindMapKind Kind, bool? Verified)> used)
    {
        ctx.Font = $"11px {family}"; ctx.TextAlign = "left"; ctx.TextBaseline = "middle"; ctx.FillStyle = "#6b675e";
        var entries = new List<(MindMapKind Kind, bool? Verified, string Label)>
        {
            (MindMapKind.Measured, true, "measured: a Qloo answer returned it"),
            (MindMapKind.Assumed, null, "assumed: not measured or checked"),
            (MindMapKind.Plain, null, "an association"),
            (MindMapKind.Measured, false, "cited to an answer that did not return it"),
        }.Where(e => used.Any(u => u.Kind == e.Kind && (e.Kind != MindMapKind.Measured || (u.Verified == false) == (e.Verified == false))));
        foreach (var (kind, verified, label) in entries)
        {
            Mark(ctx, new MindMapNode { Text = "", Kind = kind, Depth = 1, Verified = verified }, x + 5, y);
            ctx.FillStyle = "#6b675e";
            ctx.FillText(label, x + 14, y);
            x += 14 + Width(ctx, label) + 22;
        }
    }

    /// <summary>The centre's words on up to three lines of <paramref name="width"/>, and the box they need.</summary>
    private static (string[] Lines, float Width, float Height) Wrap(CanvasRenderingContext2D ctx, string text, float width, float size, string family)
    {
        ctx.Font = $"bold {size}px {family}";
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && Width(ctx, line + " " + word) > width) { lines.Add(line); line = word; }
            else line = line.Length == 0 ? word : line + " " + word;
        }
        if (line.Length > 0) lines.Add(line);
        if (lines.Count > 3) lines = [lines[0], lines[1], Fit(ctx, string.Join(' ', lines.Skip(2)), width)];
        return ([.. lines], lines.Max(l => Width(ctx, l)) + 28, lines.Count * size * 1.25f + 16);
    }

    /// <summary>The width of a run of type, measured without counting as one of the script's own measurements.</summary>
    private static float Width(CanvasRenderingContext2D ctx, string text)
    {
        using var quiet = ProbeScope.Begin();
        return Convert.ToSingle(ctx.MeasureText(text)["width"]);
    }

    private static string Fit(CanvasRenderingContext2D ctx, string text, float width)
    {
        if (Width(ctx, text) <= width) return text;
        while (text.Length > 2 && Width(ctx, text + "…") > width) text = text[..^1];
        return text.TrimEnd() + "…";
    }

    /// <summary>The words as drawn, and where they came from.</summary>
    private static (string Text, MindMapKind Kind, string? Handle, bool? Tag) Clean(string raw)
    {
        var text = raw.Trim();
        MindMapKind kind = MindMapKind.Plain;
        string? handle = null;
        if (Provenance().Match(text) is { Success: true } p)
        {
            text = text[..p.Index].TrimEnd();
            if (p.Groups[1].Success) { kind = MindMapKind.Measured; handle = "t" + p.Groups[1].Value; }
            else kind = MindMapKind.Assumed;
        }
        bool? tag = text.StartsWith('"') || text.StartsWith('“') ? true : text.StartsWith('*') ? false : null;
        text = Markup().Replace(text, "").Trim();
        // Quotes come off only when they wrap the whole label: trimmed from each end apart, '"unhurried pace" (Banshees)'
        // lost its opening quote and kept its closing one (lanternkeepermotion1).
        if (text.Length >= 2 && text[0] is '"' or '“' && text[^1] is '"' or '”' && text[1..^1].IndexOfAny(['"', '“', '”']) < 0)
            text = text[1..^1].Trim();
        return (text, kind, handle, kind == MindMapKind.Measured ? tag : null);
    }

    [GeneratedRegex(@"^\s*#\s+(.+?)\s*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^(\s*)[-*+]\s+(.+?)\s*$")]
    private static partial Regex Bullet();

    /// <summary><c>(t4)</c>, <c>(`t4`)</c> or <c>(assumed)</c> at the end of a node.</summary>
    [GeneratedRegex(@"\(\s*(?:`?t(\d{1,3})`?|assumed)\s*\)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Provenance();

    [GeneratedRegex(@"\*\*|\*|_|`")]
    private static partial Regex Markup();
    #endregion
}
