namespace Polson.Drawing.Svg;

using System;

/// <summary>Snap.svg easing and animation helpers.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// <b>Every easing is a delegate-valued property, not a method, and that is load-bearing.</b> Written
/// as methods, an easing could not be stored on an object and called through it — the natural
/// spelling for a timeline entry:
/// </para>
/// <code>
/// const t = { ease: mina.elastic };
/// t.ease(0.5);   // threw: Object type Polson.Drawing.Svg.Mina does not match target type
///                //        System.Dynamic.ExpandoObject
/// </code>
/// <para>
/// JavaScript binds <c>this</c> to the containing object, and Jint then tries to use that object as
/// the CLR receiver for the method. A delegate carries its own target, so <c>this</c> never enters
/// into it and every position works: called directly, from a local, through an object property, from
/// an array element, and bound to a <c>Func&lt;double, double&gt;</c> parameter.
/// </para>
/// <para>
/// <b>Do not turn these back into methods.</b> The signatures read as an improvement — a method is
/// the obvious shape for <c>linear(n)</c> — and the failure they reintroduce appears nowhere near
/// here, in a message naming neither easings nor the line responsible. <c>MinaInteropTests</c> pins
/// all four positions.
/// </para>
/// <para>
/// The alternative considered and rejected was a JS prelude rebinding <c>mina</c> to plain closures.
/// It fixes the call and costs the strict-member machinery: <c>mina</c> becomes an
/// <c>ExpandoObject</c>, so <c>has(mina, 'nonsense')</c> answers <c>true</c> and
/// <c>suggest(mina, 'nonsense')</c> answers <i>"'nonsense' exists on ExpandoObject — nothing to
/// correct"</i>, which is wrong advice naming a type the script author has never heard of. Measured
/// 2026-09-05; see <c>docs/motion-score-api.md</c> §5.
/// </para>
/// </remarks>
public class Mina
{
    #region Properties
    public Func<double, double> Linear { get; } = LinearCore;

    public Func<double, double> Easeout { get; } = EaseoutCore;

    public Func<double, double> Easein { get; } = EaseinCore;

    public Func<double, double> Easeinout { get; } = EaseinoutCore;

    public Func<double, double> Bounce { get; } = BounceCore;

    public Func<double, double> Elastic { get; } = ElasticCore;

    /// <summary>Overshoots backwards before easing in, matching Snap.svg's <c>mina.backin</c>.</summary>
    public Func<double, double> Backin { get; } = BackinCore;

    /// <summary>Eases out past the target and settles back, matching Snap.svg's <c>mina.backout</c>.</summary>
    public Func<double, double> Backout { get; } = BackoutCore;

    /// <summary>Milliseconds since the Unix epoch, for timing an animation trajectory.</summary>
    public Func<double> Time { get; } = TimeCore;
    #endregion

    #region Methods (private)
    static double LinearCore(double n) => n;

    static double EaseoutCore(double n) => Math.Sin(n * Math.PI / 2d);

    static double EaseinCore(double n) => 1d - Math.Cos(n * Math.PI / 2d);

    static double EaseinoutCore(double n) => 0.5d * (1d - Math.Cos(Math.PI * n));

    static double BounceCore(double n)
    {
        double s = 7.5625d, p = 2.75d;
        if (n < (1d / p))
        {
            return s * n * n;
        }
        else if (n < (2d / p))
        {
            n -= (1.5d / p);
            return s * n * n + 0.75d;
        }
        else if (n < (2.5d / p))
        {
            n -= (2.25d / p);
            return s * n * n + 0.9375d;
        }
        else
        {
            n -= (2.625d / p);
            return s * n * n + 0.984375d;
        }
    }

    static double ElasticCore(double n)
    {
        if (n == 0d || n == 1d) return n;
        double p = 0.3d, s = p / 4d;
        return Math.Pow(2d, -10d * n) * Math.Sin((n - s) * (2d * Math.PI) / p) + 1d;
    }

    static double BackinCore(double n)
    {
        if (n == 0d || n == 1d) return n;
        const double s = 1.70158d;
        return n * n * ((s + 1d) * n - s);
    }

    static double BackoutCore(double n)
    {
        if (n == 0d || n == 1d) return n;
        const double s = 1.70158d;
        n -= 1d;
        return n * n * ((s + 1d) * n + s) + 1d;
    }

    static double TimeCore() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    #endregion
}
