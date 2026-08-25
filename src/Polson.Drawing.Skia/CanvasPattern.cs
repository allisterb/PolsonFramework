namespace Polson.Drawing.Skia;

using System;
using SkiaSharp;

public enum PatternRepeat
{
    Repeat,
    RepeatX,
    RepeatY,
    NoRepeat
}

public class CanvasPattern
{
    #region Constructors
    public CanvasPattern(SKBitmap bitmap, string repetition = "repeat")
    {
        Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        Repeat = repetition.ToLowerInvariant() switch
        {
            "repeat-x" => PatternRepeat.RepeatX,
            "repeat-y" => PatternRepeat.RepeatY,
            "no-repeat" => PatternRepeat.NoRepeat,
            _ => PatternRepeat.Repeat
        };
    }
    #endregion

    #region Properties
    public SKBitmap Bitmap { get; }
    public PatternRepeat Repeat { get; }
    public SKMatrix Transform { get; set; } = SKMatrix.CreateIdentity();
    #endregion

    #region Methods
    public void setTransform(float a = 1f, float b = 0f, float c = 0f, float d = 1f, float e = 0f, float f = 0f)
    {
        Transform = new SKMatrix(a, c, e, b, d, f, 0, 0, 1);
    }

    public SKShader CreateShader()
    {
        var (tileX, tileY) = Repeat switch
        {
            PatternRepeat.RepeatX => (SKShaderTileMode.Repeat, SKShaderTileMode.Decal),
            PatternRepeat.RepeatY => (SKShaderTileMode.Decal, SKShaderTileMode.Repeat),
            PatternRepeat.NoRepeat => (SKShaderTileMode.Decal, SKShaderTileMode.Decal),
            _ => (SKShaderTileMode.Repeat, SKShaderTileMode.Repeat)
        };

        var transform = Transform;
        return SKShader.CreateBitmap(Bitmap, tileX, tileY, transform);
    }
    #endregion
}

