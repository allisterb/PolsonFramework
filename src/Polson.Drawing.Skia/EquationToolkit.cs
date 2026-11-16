// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;

/// <summary>
/// Equations from LaTeX, measured without drawing: <c>Equation.parse(latex, { size })</c>.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase spelling onto them,
/// so a script calling <c>Equation.parse(...)</c> reaches <c>Parse(...)</c>. Named <c>Equation</c>, not <c>Math</c>, which
/// would shadow JavaScript's own. Drawing is <c>ctx.fillEquation(...)</c>; this answers what an equation says and how big
/// it is before anything is placed, as <c>ctx.measureWrappedText</c> does for a paragraph.
/// </remarks>
public sealed class EquationToolkit
{
    #region Methods
    /// <summary>
    /// The equation parsed and typeset: <c>{ ok, latex, linear, width, height, ascent, descent, size }</c>, or
    /// <c>{ ok: false, error }</c> naming what the parser refused. <paramref name="options"/>: <c>{ size? }</c>, the em in
    /// canvas units (default 32).
    /// </summary>
    public Dictionary<string, object?> Parse(string latex, object? options = null)
    {
        var size = Size(options, "Equation.parse");
        var (layout, error) = EquationLayout.Parse(latex, size);
        return layout is null
            ? new() { ["ok"] = false, ["latex"] = latex, ["error"] = error }
            : Describe(layout);
    }

    /// <summary>
    /// Whether two expressions are the same mathematics, <c>Equation.equivalent(a, b?, options?)</c>, or with one argument
    /// whether an equation <c>lhs = rhs</c> holds: <c>{ verdict, a, b, detail, points, provided?, at?, values?, approximate }</c>.
    /// <c>verdict</c> is <c>'proved'</c> (simplified to 0), <c>'agrees'</c> (equal at every point tried, not proved),
    /// <c>'differs'</c> (with the counterexample <c>at</c> and both <c>values</c>), <c>'unsettled'</c>, or <c>'unread'</c>
    /// (the LaTeX was refused; <c>side</c> says which). <paramref name="options"/>: <c>{ domain?: { x: [lo, hi] }, integers?,
    /// samples?, budget? }</c>.
    /// </summary>
    public Dictionary<string, object?> Equivalent(string a, string? b = null, object? options = null)
    {
        var v = EquationCheck.Check(a ?? "", b, EquationCheck.ReadOptions(options, "Equation.equivalent"));
        // Every key, null when it does not apply: a key left out would let Jint resolve `r.values` to the dictionary's own
        // Values property, and a script would read the whole result as the two values.
        return new Dictionary<string, object?>
        {
            ["verdict"] = v.Kind,
            ["a"] = v.A,
            ["b"] = v.B,
            ["detail"] = v.Detail,
            ["points"] = v.Points,
            ["approximate"] = v.Approximate,
            ["provided"] = v.Provided,
            ["side"] = v.Side,
            ["at"] = v.At is { Count: > 0 } at ? at.ToDictionary(p => p.Key, p => (object?)p.Value) : null,
            ["values"] = v.ValueA is { } x && v.ValueB is { } y ? new object?[] { Number(x), Number(y) } : null,
        };

        // A real value as a number; a complex one as text, since a script has no complex type.
        static object Number(System.Numerics.Complex c) => Math.Abs(c.Imaginary) < 1e-12 * Math.Max(1, c.Magnitude)
            ? Math.Round(c.Real, 12)
            : $"{Math.Round(c.Real, 9).ToString(System.Globalization.CultureInfo.InvariantCulture)}{(c.Imaginary < 0 ? "−" : "+")}{Math.Round(Math.Abs(c.Imaginary), 9).ToString(System.Globalization.CultureInfo.InvariantCulture)}i";
    }
    #endregion

    #region Internal methods
    internal static Dictionary<string, object?> Describe(EquationLayout layout) => new()
    {
        ["ok"] = true,
        ["latex"] = layout.Latex,
        ["linear"] = layout.Linear,
        ["width"] = Math.Round(layout.Width, 2),
        ["height"] = Math.Round(layout.Ascent + layout.Descent, 2),
        ["ascent"] = Math.Round(layout.Ascent, 2),
        ["descent"] = Math.Round(layout.Descent, 2),
        ["size"] = layout.Size,
    };

    internal static float Size(object? options, string method)
    {
        var o = JsInterop.AsDict(options);
        if (o is null) return DefaultSize;
        foreach (var key in o.Keys)
            if (key?.ToString() != "size") throw new ArgumentException($"{method}'s options are {{ size }}; '{key}' is not one.");
        return o["size"] is { } s ? Convert.ToSingle(s, System.Globalization.CultureInfo.InvariantCulture) : DefaultSize;
    }
    #endregion

    #region Fields
    internal const float DefaultSize = 32;
    #endregion
}
