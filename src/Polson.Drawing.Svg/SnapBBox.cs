namespace Polson.Drawing.Svg;

using System;
using System.Globalization;

/// <summary>Axis-aligned bounding box returned by element measurement.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SnapBBox
{
    #region Constructors
    public SnapBBox(float x = 0, float y = 0, float width = 0, float height = 0)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
    #endregion

    #region Properties
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    public float W => Width;
    public float H => Height;
    public float X2 => X + Width;
    public float Y2 => Y + Height;
    public float Cx => X + Width / 2f;
    public float Cy => Y + Height / 2f;
    public float R0 => MathF.Sqrt(Width * Width + Height * Height) / 2f;
    public float R1 => MathF.Min(Width, Height) / 2f;
    public float R2 => MathF.Max(Width, Height) / 2f;

    public string Path => string.Create(CultureInfo.InvariantCulture, $"M{X},{Y}h{Width}v{Height}h{-Width}z");
    public string Vb => string.Create(CultureInfo.InvariantCulture, $"{X} {Y} {Width} {Height}");
    #endregion

    #region Methods
    /// <summary>What <c>JSON.stringify</c> writes: Snap's own fields, in Snap's spelling.</summary>
    /// <remarks>Without it the stringify walked the CLR object and logged <c>Width</c>, <c>X2</c>, <c>R0</c>, so an agent
    /// took <c>.width</c> to be missing (lanternkeeperlang2).</remarks>
    public System.Collections.Generic.Dictionary<string, object?> ToJSON(string? key = null) => new()
    {
        ["x"] = X, ["y"] = Y, ["width"] = Width, ["height"] = Height, ["w"] = W, ["h"] = H,
        ["x2"] = X2, ["y2"] = Y2, ["cx"] = Cx, ["cy"] = Cy, ["r0"] = R0, ["r1"] = R1, ["r2"] = R2,
        ["path"] = Path, ["vb"] = Vb,
    };

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"x: {X}, y: {Y}, width: {Width}, height: {Height}");
    #endregion
}

