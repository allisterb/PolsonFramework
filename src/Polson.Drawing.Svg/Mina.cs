namespace Polson.Drawing.Svg;

using System;

/// <summary>Snap.svg easing and animation helpers.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class Mina
{
    #region Methods
    public float Linear(float n) => n;

    public float Easeout(float n) => MathF.Sin(n * MathF.PI / 2f);

    public float Easein(float n) => 1f - MathF.Cos(n * MathF.PI / 2f);

    public float Easeinout(float n) => 0.5f * (1f - MathF.Cos(MathF.PI * n));

    public float Bounce(float n)
    {
        float s = 7.5625f, p = 2.75f;
        if (n < (1f / p))
        {
            return s * n * n;
        }
        else if (n < (2f / p))
        {
            n -= (1.5f / p);
            return s * n * n + 0.75f;
        }
        else if (n < (2.5f / p))
        {
            n -= (2.25f / p);
            return s * n * n + 0.9375f;
        }
        else
        {
            n -= (2.625f / p);
            return s * n * n + 0.984375f;
        }
    }

    public float Elastic(float n)
    {
        if (n == 0f || n == 1f) return n;
        float p = 0.3f, s = p / 4f;
        return MathF.Pow(2f, -10f * n) * MathF.Sin((n - s) * (2f * MathF.PI) / p) + 1f;
    }
    #endregion
}

