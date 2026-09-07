namespace Polson.Drawing.Svg;

using System;
using System.Globalization;

using global::Svg;

/// <summary>
/// A line ending — an arrowhead, a dot, a bar — living in <c>&lt;defs&gt;</c> and referenced by url.
/// </summary>
/// <remarks>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>marker.url</c> reaches <c>Url</c>.
/// </para>
/// <para>
/// <c>marker-end</c> has always rendered — it is pinned in the CSS support matrix — but reaching it
/// meant building an <c>SvgMarker</c> by hand with <c>refX</c>, <c>refY</c>, <c>orient</c> and a
/// child path in the marker's own coordinate space. That is four things to get right for an
/// arrowhead, and getting <c>refX</c> wrong puts the head beside the line rather than on its end.
/// Both Apollo plates are covered in dimension lines with arrowheads, every one hand-rolled.
/// </para>
/// </remarks>
public class SnapMarker : SnapElement
{
    #region Constructors
    public SnapMarker(SvgMarker marker, SnapPaper? paper = null) : base(marker, paper) { }
    #endregion

    #region Properties
    /// <summary>The <c>url(#id)</c> to put in <c>marker-end</c>, <c>marker-start</c> or <c>marker-mid</c>.</summary>
    /// <remarks>
    /// The same convenience <see cref="SnapFilter.Url"/> offers, and for the same reason: the id is
    /// generated, so assembling the reference by hand is a step that can only go wrong.
    /// </remarks>
    public string Url => "url(#" + Node.ID + ")";
    #endregion
}

/// <summary>Builds the marker shapes.</summary>
/// <remarks>
/// Separate from <see cref="SnapMarker"/> so the geometry is testable without a paper, and so the
/// styles can be listed in one place for the error message when an unknown one is asked for.
/// </remarks>
internal static class SnapMarkerShapes
{
    #region Methods
    /// <summary>
    /// The marker's path in a box of <paramref name="size"/>, and where on it the line should end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every shape is drawn in its own coordinate space running <c>0..size</c> on both axes, with the
    /// line arriving horizontally from the left. <c>refX</c> is the point that lands on the vertex
    /// being marked — the tip for an arrow, the centre for a dot — and it is returned alongside the
    /// path because the two are one decision and separating them is how a head ends up floating
    /// beside its line.
    /// </para>
    /// </remarks>
    internal static (string Path, float RefX, float RefY, bool Filled) For(string style, float size)
    {
        var s = size.ToString(CultureInfo.InvariantCulture);
        var h = (size / 2f).ToString(CultureInfo.InvariantCulture);
        var q = (size / 4f).ToString(CultureInfo.InvariantCulture);

        return style.ToLowerInvariant() switch
        {
            // A solid triangle, tip at the right so it points along the line's direction.
            "arrow" => ($"M0,0 L{s},{h} L0,{s} z", size, size / 2f, true),
            // Concave back, which reads sharper at small sizes than the plain triangle.
            "barb" => ($"M0,0 L{s},{h} L0,{s} L{q},{h} z", size, size / 2f, true),
            // Two strokes rather than a filled shape: an open head, for technical drawing.
            "open" => ($"M0,0 L{s},{h} L0,{s}", size, size / 2f, false),
            "dot" => ($"M{h},0 A{h},{h} 0 1,1 {h},{s} A{h},{h} 0 1,1 {h},0 z", size / 2f, size / 2f, true),
            // The terminator on a dimension line.
            "bar" => ($"M{h},0 L{h},{s}", size / 2f, size / 2f, false),
            _ => throw new ArgumentException(
                $"'{style}' is not a marker style. Supported: arrow, barb, open, dot, bar.", nameof(style))
        };
    }
    #endregion
}
