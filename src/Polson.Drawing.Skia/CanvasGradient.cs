namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

public enum GradientType
{
    Linear,
    Radial,
    Conic
}

public class CanvasGradient
{
    #region Constructors
    public CanvasGradient(float x0, float y0, float x1, float y1)
    {
        Type = GradientType.Linear;
        StartPoint = new SKPoint(x0, y0);
        EndPoint = new SKPoint(x1, y1);
    }

    public CanvasGradient(float x0, float y0, float r0, float x1, float y1, float r1)
    {
        Type = GradientType.Radial;
        StartPoint = new SKPoint(x0, y0);
        StartRadius = r0;
        EndPoint = new SKPoint(x1, y1);
        EndRadius = r1;
    }

    public CanvasGradient(float startAngle, float x, float y)
    {
        Type = GradientType.Conic;
        StartPoint = new SKPoint(x, y);
        StartAngle = startAngle;
    }
    #endregion

    #region Properties
    public GradientType Type { get; }
    public SKPoint StartPoint { get; }
    public SKPoint EndPoint { get; }
    public float StartRadius { get; }
    public float EndRadius { get; }
    public float StartAngle { get; }
    #endregion

    #region Methods
    public CanvasGradient addColorStop(float offset, string color)
    {
        var skColor = SkiaColorParser.Parse(color);
        _colorStops.Add((Math.Clamp(offset, 0f, 1f), skColor));
        _colorStops.Sort((a, b) => a.offset.CompareTo(b.offset));
        return this;
    }

    public SKShader CreateShader(float globalAlpha = 1f)
    {
        if (_colorStops.Count == 0)
        {
            return SKShader.CreateColor(SkiaColorParser.ApplyAlpha(SKColors.Black, globalAlpha));
        }

        var stops = _colorStops;
        if (stops.Count == 1)
        {
            return SKShader.CreateColor(SkiaColorParser.ApplyAlpha(stops[0].color, globalAlpha));
        }

        var colors = stops.Select(s => SkiaColorParser.ApplyAlpha(s.color, globalAlpha)).ToArray();
        var positions = stops.Select(s => s.offset).ToArray();

        return Type switch
        {
            GradientType.Linear => SKShader.CreateLinearGradient(
                StartPoint, EndPoint, colors, positions, SKShaderTileMode.Clamp),

            GradientType.Radial => SKShader.CreateTwoPointConicalGradient(
                StartPoint, StartRadius, EndPoint, EndRadius, colors, positions, SKShaderTileMode.Clamp),

            GradientType.Conic => SKShader.CreateSweepGradient(
                StartPoint, colors, positions, SKShaderTileMode.Clamp,
                StartAngle * 180f / MathF.PI, (StartAngle * 180f / MathF.PI) + 360f),

            _ => SKShader.CreateColor(SKColors.Black)
        };
    }
    #endregion

    #region Fields
    private readonly List<(float offset, SKColor color)> _colorStops = [];
    #endregion
}

