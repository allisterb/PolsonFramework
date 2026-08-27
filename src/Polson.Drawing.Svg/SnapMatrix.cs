namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

/// <summary>Snap.svg transform matrix.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SnapMatrix
{
    #region Constructors
    public SnapMatrix(float a = 1f, float b = 0f, float c = 0f, float d = 1f, float e = 0f, float f = 0f)
    {
        A = a;
        B = b;
        C = c;
        D = d;
        E = e;
        F = f;
    }

    public SnapMatrix(SnapMatrix other)
    {
        ArgumentNullException.ThrowIfNull(other);
        A = other.A;
        B = other.B;
        C = other.C;
        D = other.D;
        E = other.E;
        F = other.F;
    }
    #endregion

    #region Properties
    public float A { get; set; } = 1f;
    public float B { get; set; } = 0f;
    public float C { get; set; } = 0f;
    public float D { get; set; } = 1f;
    public float E { get; set; } = 0f;
    public float F { get; set; } = 0f;

    public float Determinant => A * D - B * C;
    public bool IsIdentity => A == 1f && B == 0f && C == 0f && D == 1f && E == 0f && F == 0f;
    #endregion

    #region Methods
    public SnapMatrix Add(SnapMatrix other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Add(other.A, other.B, other.C, other.D, other.E, other.F);
    }

    public SnapMatrix Add(float a, float b, float c, float d, float e, float f)
    {
        var aNew = a * A + b * C;
        var bNew = a * B + b * D;
        E += e * A + f * C;
        F += e * B + f * D;
        C = c * A + d * C;
        D = c * B + d * D;
        A = aNew;
        B = bNew;
        return this;
    }

    public SnapMatrix MultLeft(SnapMatrix other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return MultLeft(other.A, other.B, other.C, other.D, other.E, other.F);
    }

    public SnapMatrix MultLeft(float a, float b, float c, float d, float e, float f)
    {
        var aNew = a * A + c * B;
        var cNew = a * C + c * D;
        var eNew = a * E + c * F + e;
        B = b * A + d * B;
        D = b * C + d * D;
        F = b * E + d * F + f;
        A = aNew;
        C = cNew;
        E = eNew;
        return this;
    }

    public SnapMatrix Translate(float x, float y) =>
        Add(1f, 0f, 0f, 1f, x, y);

    public SnapMatrix Scale(float sx, float? sy = null, float cx = 0f, float cy = 0f)
    {
        var scaleY = sy ?? sx;
        if (cx != 0f || cy != 0f)
        {
            Translate(cx, cy);
            Add(sx, 0f, 0f, scaleY, 0f, 0f);
            return Translate(-cx, -cy);
        }
        return Add(sx, 0f, 0f, scaleY, 0f, 0f);
    }

    public SnapMatrix Rotate(float deg, float cx = 0f, float cy = 0f)
    {
        var rad = deg * MathF.PI / 180f;
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);
        if (cx != 0f || cy != 0f)
        {
            Translate(cx, cy);
            Add(cos, sin, -sin, cos, 0f, 0f);
            return Translate(-cx, -cy);
        }
        return Add(cos, sin, -sin, cos, 0f, 0f);
    }

    public SnapMatrix SkewX(float deg)
    {
        var rad = deg * MathF.PI / 180f;
        var tan = MathF.Tan(rad);
        return Add(1f, 0f, tan, 1f, 0f, 0f);
    }

    public SnapMatrix SkewY(float deg)
    {
        var rad = deg * MathF.PI / 180f;
        var tan = MathF.Tan(rad);
        return Add(1f, tan, 0f, 1f, 0f, 0f);
    }

    public SnapMatrix Invert()
    {
        var det = Determinant;
        if (MathF.Abs(det) < 1e-9f)
            throw new InvalidOperationException("Matrix is not invertible.");

        var invDet = 1f / det;
        var newA = D * invDet;
        var newB = -B * invDet;
        var newC = -C * invDet;
        var newD = A * invDet;
        var newE = (C * F - D * E) * invDet;
        var newF = (B * E - A * F) * invDet;

        A = newA;
        B = newB;
        C = newC;
        D = newD;
        E = newE;
        F = newF;
        return this;
    }

    public (float X, float Y) TransformPoint(float x, float y) =>
        (A * x + C * y + E, B * x + D * y + F);

    public SnapPoint TransformPoint(SnapPoint pt)
    {
        var (x, y) = TransformPoint(pt.X, pt.Y);
        return new SnapPoint(x, y, pt.Alpha, pt.M, pt.N);
    }

    public SnapMatrix Clone() => new(this);

    public string ToTransformString() =>
        string.Create(CultureInfo.InvariantCulture, $"matrix({A},{B},{C},{D},{E},{F})");

    public SvgMatrix ToSvgMatrix() =>
        new(new List<float> { A, B, C, D, E, F });

    public SKMatrix ToSkMatrix() =>
        new(A, C, E, B, D, F, 0f, 0f, 1f);

    public override string ToString() => ToTransformString();
    #endregion
}

