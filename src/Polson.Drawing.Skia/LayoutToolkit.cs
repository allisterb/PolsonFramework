namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Rectangle arithmetic for composing a page: dividing a canvas into panels, padding them, and
/// stacking measured blocks inside them.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// This is deliberately <b>not</b> a layout engine. There is no document, no flow, no cascade and
/// nothing measures itself — every method takes rectangles and returns rectangles, and the caller
/// decides what goes in them. Content-driven sizing comes from
/// <c>ctx.measureWrappedText(...)</c>, whose reported height is what
/// <see cref="Stack"/> consumes. Keeping the two apart is what lets the same geometry serve a
/// canvas, an SVG paper, or a set of comic panels.
/// </para>
/// </remarks>
public class LayoutToolkit
{
    #region Methods
    /// <summary>Builds a rectangle. Accepts the same shape every method here returns.</summary>
    public Dictionary<string, object> Rect(float x, float y, float width, float height) =>
        Make(x, y, width, height);

    /// <summary>
    /// Shrinks a rectangle inward by padding, CSS-shorthand style.
    /// </summary>
    /// <remarks>
    /// One value pads all four sides, two are vertical then horizontal, three are top / horizontal /
    /// bottom, four are top / right / bottom / left — the order a stylesheet uses, so the figures
    /// from a design spec transfer without rearranging. Padding larger than the rectangle collapses
    /// it to zero rather than going negative, because a negative extent silently inverts every
    /// comparison downstream.
    /// </remarks>
    public Dictionary<string, object> Inset(object rect, float top, float? right = null,
        float? bottom = null, float? left = null)
    {
        var r = AsRect(rect);
        var (t, rt, b, l) = (top, right ?? top, bottom ?? top, left ?? right ?? top);

        var width = Math.Max(0f, r.Width - l - rt);
        var height = Math.Max(0f, r.Height - t - b);
        return Make(r.X + l, r.Y + t, width, height);
    }

    /// <summary>Expands a rectangle outward. The inverse of <see cref="Inset"/>.</summary>
    public Dictionary<string, object> Outset(object rect, float top, float? right = null,
        float? bottom = null, float? left = null) =>
        Inset(rect, -top, right is null ? -top : -right, bottom is null ? -top : -bottom,
            left is null ? (right is null ? -top : -right) : -left);

    /// <summary>
    /// Divides a rectangle into horizontal bands.
    /// </summary>
    /// <remarks>
    /// <paramref name="divisions"/> is either a count for equal bands, or an array of weights for
    /// proportional ones — <c>[70, 20, 10]</c> is Manual 09's big/medium/small law, and the weights
    /// need not sum to anything in particular. Gaps are taken out of the total before dividing, so
    /// the bands always fit the rectangle exactly.
    /// </remarks>
    public Dictionary<string, object>[] Rows(object rect, object divisions, float gap = 0f)
    {
        var r = AsRect(rect);
        var weights = Weights(divisions);
        var offsets = Distribute(r.Height, weights, gap);

        return [.. offsets.Select(o => Make(r.X, r.Y + o.Offset, r.Width, o.Extent))];
    }

    /// <summary>Divides a rectangle into vertical bands. See <see cref="Rows"/>.</summary>
    public Dictionary<string, object>[] Columns(object rect, object divisions, float gap = 0f)
    {
        var r = AsRect(rect);
        var weights = Weights(divisions);
        var offsets = Distribute(r.Width, weights, gap);

        return [.. offsets.Select(o => Make(r.X + o.Offset, r.Y, o.Extent, r.Height))];
    }

    /// <summary>
    /// Divides a rectangle into a grid, row-major.
    /// </summary>
    /// <remarks>
    /// Cell <c>(row, column)</c> is at index <c>row * columns + column</c>. Pass
    /// <paramref name="rowGap"/> to separate the axes; it defaults to <paramref name="gap"/>.
    /// </remarks>
    public Dictionary<string, object>[] Grid(object rect, int columns, int rows, float gap = 0f,
        float? rowGap = null)
    {
        if (columns < 1 || rows < 1)
        {
            throw new ArgumentException("Layout.grid(...) needs at least one column and one row.");
        }

        var r = AsRect(rect);
        var xs = Distribute(r.Width, Enumerable.Repeat(1f, columns).ToArray(), gap);
        var ys = Distribute(r.Height, Enumerable.Repeat(1f, rows).ToArray(), rowGap ?? gap);

        var cells = new List<Dictionary<string, object>>(columns * rows);
        foreach (var row in ys)
        {
            foreach (var column in xs)
            {
                cells.Add(Make(r.X + column.Offset, r.Y + row.Offset, column.Extent, row.Extent));
            }
        }
        return [.. cells];
    }

    /// <summary>
    /// Stacks boxes of <b>known heights</b> down a rectangle, returning one rectangle each.
    /// </summary>
    /// <remarks>
    /// The counterpart to <c>ctx.measureWrappedText(...)</c>: measure each block, stack the heights,
    /// then draw into the rectangles you get back. Unlike <see cref="Rows"/>, the sizes come from
    /// the content rather than from the container, which is what a caption under a chart or a
    /// paragraph under a heading actually needs.
    /// <para>
    /// Items are placed even when they run past the bottom of <paramref name="rect"/>. Overflow is
    /// a fact worth seeing — compare the last rectangle's <c>y2</c> against the container's, rather
    /// than having the toolkit quietly clip or compress.
    /// </para>
    /// </remarks>
    public Dictionary<string, object>[] Stack(object rect, object heights, float gap = 0f)
    {
        var r = AsRect(rect);
        var sizes = Numbers(heights);
        var stacked = new List<Dictionary<string, object>>(sizes.Length);

        var cursor = r.Y;
        foreach (var height in sizes)
        {
            stacked.Add(Make(r.X, cursor, r.Width, height));
            cursor += height + gap;
        }
        return [.. stacked];
    }

    /// <summary>Centres a box of the given size inside a rectangle.</summary>
    public Dictionary<string, object> Center(object rect, float width, float height) =>
        Place(rect, width, height, "center");

    /// <summary>
    /// Places a box of the given size inside a rectangle at one of the nine anchors.
    /// </summary>
    /// <remarks>
    /// <paramref name="align"/> is <c>'topLeft'</c>, <c>'top'</c>, <c>'topRight'</c>, <c>'left'</c>,
    /// <c>'center'</c>, <c>'right'</c>, <c>'bottomLeft'</c>, <c>'bottom'</c> or
    /// <c>'bottomRight'</c>. Case and separators are ignored, so <c>'bottom-right'</c> also works.
    /// </remarks>
    public Dictionary<string, object> Place(object rect, float width, float height, string align = "center")
    {
        var r = AsRect(rect);
        var key = new string((align ?? "center").Where(char.IsLetter).ToArray()).ToLowerInvariant();

        var x = key switch
        {
            "topleft" or "left" or "bottomleft" => r.X,
            "topright" or "right" or "bottomright" => r.X + r.Width - width,
            _ => r.X + (r.Width - width) / 2f
        };
        var y = key switch
        {
            "topleft" or "top" or "topright" => r.Y,
            "bottomleft" or "bottom" or "bottomright" => r.Y + r.Height - height,
            _ => r.Y + (r.Height - height) / 2f
        };

        return Make(x, y, width, height);
    }

    /// <summary>The smallest rectangle containing every rectangle given.</summary>
    /// <remarks>
    /// What a clear-space guide or a lockup bounding box is measured from, once the pieces are
    /// placed. Empty input returns a zero rectangle at the origin rather than throwing, so a
    /// filtered list that came back empty does not take the whole script down.
    /// </remarks>
    public Dictionary<string, object> Bounds(object rects)
    {
        var all = AsRects(rects);
        if (all.Count == 0) return Make(0f, 0f, 0f, 0f);

        var minX = all.Min(r => r.X);
        var minY = all.Min(r => r.Y);
        var maxX = all.Max(r => r.X + r.Width);
        var maxY = all.Max(r => r.Y + r.Height);

        return Make(minX, minY, maxX - minX, maxY - minY);
    }
    #endregion

    #region Child types
    internal readonly record struct RectValue(float X, float Y, float Width, float Height);

    private readonly record struct Band(float Offset, float Extent);
    #endregion

    #region Methods (private)
    internal static Dictionary<string, object> Make(float x, float y, float width, float height) =>
        new()
        {
            ["x"] = x,
            ["y"] = y,
            ["width"] = width,
            ["height"] = height,
            ["x2"] = x + width,
            ["y2"] = y + height,
            ["cx"] = x + width / 2f,
            ["cy"] = y + height / 2f
        };

    /// <summary>
    /// Reads any <c>{ x, y, width, height }</c> object as a rectangle.
    /// </summary>
    /// <remarks>
    /// Deliberately structural rather than typed: this is what makes the toolkit compose with
    /// <c>element.getBBox()</c>, <c>Drawing.subdivideProportions(...)</c>,
    /// <c>ctx.measureWrappedText(...)</c> and a hand-written literal without any of them knowing
    /// about each other.
    /// </remarks>
    internal static RectValue AsRect(object rect)
    {
        var dict = JsInterop.AsDict(rect)
            ?? throw new ArgumentException("Layout expects a { x, y, width, height } rectangle.");

        return new RectValue(Field(dict, "x"), Field(dict, "y"), Field(dict, "width"), Field(dict, "height"));
    }

    private static List<RectValue> AsRects(object rects)
    {
        var list = new List<RectValue>();
        if (rects is IEnumerable items and not string)
        {
            foreach (var item in items)
            {
                if (item is not null) list.Add(AsRect(item));
            }
        }
        else if (rects is not null)
        {
            list.Add(AsRect(rects));
        }
        return list;
    }

    private static float Field(IDictionary dict, string name) =>
        dict.Contains(name) ? Convert.ToSingle(dict[name], CultureInfo.InvariantCulture) : 0f;

    /// <summary>A count becomes equal weights; an array is taken as the weights themselves.</summary>
    private static float[] Weights(object divisions)
    {
        if (divisions is IEnumerable and not string)
        {
            var supplied = Numbers(divisions);
            if (supplied.Length == 0)
            {
                throw new ArgumentException("Layout received an empty weight list.");
            }
            return supplied;
        }

        var count = Convert.ToInt32(divisions, CultureInfo.InvariantCulture);
        if (count < 1)
        {
            throw new ArgumentException("Layout needs at least one division.");
        }
        return [.. Enumerable.Repeat(1f, count)];
    }

    private static float[] Numbers(object values)
    {
        if (values is IEnumerable items and not string)
        {
            var list = new List<float>();
            foreach (var item in items) list.Add(Convert.ToSingle(item, CultureInfo.InvariantCulture));
            return [.. list];
        }
        return [Convert.ToSingle(values, CultureInfo.InvariantCulture)];
    }

    /// <summary>
    /// Splits an extent into weighted bands with gaps between them.
    /// </summary>
    /// <remarks>
    /// Gaps come out of the total first, so the bands always sum back to the container — the
    /// alternative, dividing first and then inserting gaps, overflows by exactly the gap total and
    /// is the commonest way a hand-rolled grid drifts off its page. A gap total exceeding the
    /// extent yields zero-width bands rather than negative ones.
    /// </remarks>
    private static Band[] Distribute(float extent, float[] weights, float gap)
    {
        var available = Math.Max(0f, extent - gap * (weights.Length - 1));
        var total = weights.Sum();
        if (total <= 0f) total = weights.Length;

        var bands = new Band[weights.Length];
        var cursor = 0f;
        for (var i = 0; i < weights.Length; i++)
        {
            var size = available * (weights[i] / total);
            bands[i] = new Band(cursor, size);
            cursor += size + gap;
        }
        return bands;
    }
    #endregion
}
