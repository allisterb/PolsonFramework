namespace Polson.Animation;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using SkiaSharp;

/// <summary>
/// How a layer combines with what is under it: Synfig's blend methods, ported formula by formula.
/// </summary>
/// <remarks>
/// <b>Ported from Synfig</b> (<c>synfig-core/src/synfig/color/colorblendingfunctions.h</c>, GPL-2-or-later),
/// one SkSL blender per method. Synfig blends in straight (unpremultiplied) colour and passes the layer's
/// <c>amount</c> into the formula, so each blender unpremultiplies both pixels, applies Synfig's arithmetic
/// with <c>amount</c> as a uniform, and premultiplies the result. Most methods keep the backdrop's alpha —
/// a multiplied layer cannot paint where nothing was — which is where they differ from the CSS modes of
/// the same name. <c>composite</c> needs no blender: it is Skia's source-over.
/// <para>
/// Not ported: <c>straight</c> and <c>alpha</c>, which clear everything outside the layer and so depend on
/// Synfig's task bounds; the YUV methods (<c>color</c>, <c>hue</c>, <c>saturation</c>, <c>luminance</c>);
/// and the deprecated ones. Each is refused by name.
/// </para>
/// </remarks>
internal sealed class MotionBlend
{
    #region Constructors
    private MotionBlend(string name, int synfig, string? body, string? css)
    {
        Name = name;
        Synfig = synfig;
        this.body = body;
        Css = css;
    }
    #endregion

    #region Fields
    private static readonly ConcurrentDictionary<string, SKRuntimeEffect> Effects = new();
    private readonly string? body;

    // The straight colour a of the layer, b of the backdrop; sa, da their alphas; k the layer's amount.
    // Each body leaves the result's straight colour in c and its alpha in oa.
    private const string Onto = "float as = sa * k; c = x * as + b * (1 - as); oa = da;";

    private static readonly MotionBlend[] All =
    [
        new("composite", 0, null, null),
        new("onto", 13, "float3 x = a; " + Onto, null),
        new("behind", 12, """
            float sa2 = sa == 0 ? 0.000001 * k : sa * k;
            oa = da + sa2 * (1 - da);
            c = oa > 0.000001 ? (b * da + a * sa2 * (1 - da)) / oa : float3(0);
            """, null),
        new("multiply", 6, "float m = k * sa; c = b + (b * a - b) * m; oa = da;", "multiply"),
        new("divide", 7, "float m = k * sa; c = b + (b / (a + 0.000001) - b) * m; oa = da;", null),
        new("screen", 16, "float3 x = 1 - (1 - a) * (1 - b); " + Onto, "screen"),
        new("overlay", 20, """
            float3 rm = b * a;
            float3 rs = 1 - (1 - a) * (1 - b);
            float3 x = a * rs + (1 - a) * rm;
            """ + Onto, "overlay"),
        new("hardLight", 17, """
            float3 x = float3(
                a.r > 0.5 ? 1 - (1 - (a.r * 2 - 1)) * (1 - b.r) : b.r * (a.r * 2),
                a.g > 0.5 ? 1 - (1 - (a.g * 2 - 1)) * (1 - b.g) : b.g * (a.g * 2),
                a.b > 0.5 ? 1 - (1 - (a.b * 2 - 1)) * (1 - b.b) : b.b * (a.b * 2));
            """ + Onto, "hard-light"),
        new("brighten", 2, "float al = sa * k; c = max(b, a * al); oa = da;", "lighten"),
        new("darken", 3, "float al = sa * k; c = min(b, (a - 1) * al + 1); oa = da;", "darken"),
        new("add", 4, "c = b * da + a * (sa * k); oa = da;", null),
        new("subtract", 5, "c = b * da - a * (sa * k); oa = da;", null),
        new("difference", 18, "c = abs(b * da - a * (sa * k)); oa = da;", "difference"),
        new("alphaOver", 19, "c = b; oa = da * (1 - sa * k);", null),
    ];

    private static readonly Dictionary<string, string> NotPorted = new(StringComparer.OrdinalIgnoreCase)
    {
        ["straight"] = "it replaces everything outside the layer as well, which depends on Synfig's render bounds",
        ["alpha"] = "it clears everything outside the layer, which depends on Synfig's render bounds",
        ["color"] = "Synfig's YUV methods are not ported yet",
        ["hue"] = "Synfig's YUV methods are not ported yet",
        ["saturation"] = "Synfig's YUV methods are not ported yet",
        ["luminance"] = "Synfig's YUV methods are not ported yet",
    };
    #endregion

    #region Properties
    public static MotionBlend Composite => All[0];

    /// <summary>The name a script uses.</summary>
    public string Name { get; }

    /// <summary>Synfig's <c>BLEND_*</c> number, written as <c>blend_method</c>.</summary>
    public int Synfig { get; }

    /// <summary>The CSS <c>mix-blend-mode</c> that agrees with this method over an opaque backdrop, or null.</summary>
    public string? Css { get; }

    public bool IsComposite => body is null;
    #endregion

    #region Methods
    /// <summary>The method a script named, or a refusal listing the ones there are.</summary>
    public static MotionBlend Parse(object? value, string who)
    {
        if (value is null) return Composite;
        var name = value as string ?? throw new ArgumentException($"{who}'s blend is a name: {Names()}.");
        if (All.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)) is { } found) return found;
        if (NotPorted.TryGetValue(name, out var why))
            throw new ArgumentException($"{who}'s blend '{name}' is not available: {why}. It takes: {Names()}.");
        throw new ArgumentException($"{who} has no blend '{name}'. It takes: {Names()}.");
    }

    /// <summary>The blender that composites a layer drawn at full strength onto its backdrop at <paramref name="amount"/>.</summary>
    public SKBlender Blender(double amount)
    {
        if (body is null) throw new InvalidOperationException("composite needs no blender.");
        var effect = Effects.GetOrAdd(Name, _ =>
        {
            var source = $$"""
                uniform float amount;
                half4 main(half4 srcIn, half4 dstIn) {
                    float4 src = float4(srcIn);
                    float4 dst = float4(dstIn);
                    float sa = src.a;
                    float da = dst.a;

                    // Synfig blends only where the layer has something; add, subtract and difference would
                    // otherwise scale a part-transparent backdrop's colour by its alpha everywhere.
                    if (sa <= 0) return dstIn;
                    float k = amount;
                    float3 a = sa > 0 ? src.rgb / sa : float3(0);
                    float3 b = da > 0 ? dst.rgb / da : float3(0);
                    float3 c = b;
                    float oa = da;
                    {{body}}
                    // Colour is left unclamped, as on Synfig's float surfaces; an 8-bit surface clamps on store.
                    oa = clamp(oa, 0, 1);
                    return half4(c * oa, oa);
                }
                """;
            return SKRuntimeEffect.CreateBlender(source, out var errors)
                ?? throw new InvalidOperationException($"blend '{Name}' did not compile: {errors}");
        });

        var uniforms = new SKRuntimeEffectUniforms(effect) { ["amount"] = (float)amount };
        return effect.ToBlender(uniforms);
    }

    private static string Names() => string.Join(", ", All.Select(b => b.Name));
    #endregion
}
