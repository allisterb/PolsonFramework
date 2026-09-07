namespace Polson.Drawing.Svg;

/// <summary>
/// The <c>Snap.brush</c> namespace: nib construction, with the knobs.
/// </summary>
/// <remarks>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>Snap.brush.split(6, 20)</c> reaches
/// <see cref="Split"/>.
/// </para>
/// <para>
/// <b><c>Snap.brush</c> is callable and a namespace at once</b>, exactly as <c>Snap</c> itself is:
/// <c>Snap.brush('taper')</c> is the one-call form, and <c>Snap.brush.taper(20, 1.4)</c> is the same
/// nib with its profile tuned. Two separate names would have been the alternative, and an agent
/// having to remember which of them takes arguments is worse than one name that does both.
/// </para>
/// <para>
/// The factories live here rather than as public statics on <see cref="SnapBrush"/> because a static
/// on the nib type reads as <c>nib.taper</c> on the documented surface — a call no script can make,
/// since it is not a member of a nib you are holding.
/// </para>
/// </remarks>
public class SnapBrushApi
{
    #region Properties
    /// <summary>Every preset name, so a script can offer them without hard-coding the list.</summary>
    public string[] Presets => SnapBrush.PresetNames;
    #endregion

    #region Methods
    /// <summary>A leaf: nothing at both ends, fullest in the middle. The default nib.</summary>
    /// <remarks><paramref name="fullness"/> below 1 holds the width out toward the ends; above 1
    /// pinches it toward the centre.</remarks>
    public SnapBrush Taper(float width = 14f, float fullness = 1f, int steps = 64) =>
        SnapBrush.Taper(width, fullness, steps);

    /// <summary>Full at the start, tapering to a point — a stroke that lands and lifts.</summary>
    public SnapBrush Wedge(float width = 14f, float fullness = 1f, int steps = 64) =>
        SnapBrush.Wedge(width, fullness, steps);

    /// <summary>A flat nib held at an angle: constant width, ends cut on the skew.</summary>
    public SnapBrush Chisel(float width = 12f, float skew = 18f) => SnapBrush.Chisel(width, skew);

    /// <summary>A dry, split nib: separate ribbons with staggered ends, so the gaps are real holes.</summary>
    public SnapBrush Split(int ribbons = 4, float width = 16f, float fullness = 1.3f, int steps = 40) =>
        SnapBrush.Split(ribbons, width, fullness, steps);

    /// <summary>A nib from your own outline, as an SVG <c>d</c> string.</summary>
    public SnapBrush FromPath(string templatePathData, string name = "brush", float sampleStep = 0.75f) =>
        SnapBrush.FromPath(templatePathData, name, sampleStep);

    /// <summary>A nib from an element's geometry, so a drawn shape becomes the brush.</summary>
    public SnapBrush FromElement(SnapElement element, string name = "brush", float sampleStep = 0.75f) =>
        SnapBrush.FromElement(element, name, sampleStep);

    /// <summary>A preset by name. An unknown name throws and lists the real ones.</summary>
    public SnapBrush Preset(string name) => SnapBrush.Preset(name);

    /// <summary>Whether <paramref name="name"/> is a preset.</summary>
    public bool HasPreset(string? name) => SnapBrush.HasPreset(name);
    #endregion
}
