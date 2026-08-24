namespace Polson.Drawing.Svg;

using System;
using System.Globalization;

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
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"x: {X}, y: {Y}, width: {Width}, height: {Height}");
    #endregion
}

