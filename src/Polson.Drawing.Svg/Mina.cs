namespace Polson.Drawing.Svg;

using System;

public class Mina
{
    #region Methods
    public float linear(float n) => n;

    public float easeout(float n) => MathF.Sin(n * MathF.PI / 2f);

    public float easein(float n) => 1f - MathF.Cos(n * MathF.PI / 2f);

    public float easeinout(float n) => 0.5f * (1f - MathF.Cos(MathF.PI * n));

    public float bounce(float n)
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

    public float elastic(float n)
    {
        if (n == 0f || n == 1f) return n;
        float p = 0.3f, s = p / 4f;
        return MathF.Pow(2f, -10f * n) * MathF.Sin((n - s) * (2f * MathF.PI) / p) + 1f;
    }
    #endregion
}

