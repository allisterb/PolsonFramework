namespace Polson.Drawing.Skia;

using System;
using SkiaSharp;

/// <summary>
/// A configured drawing medium: what the mark is coloured with, how the path is textured, and how
/// its edge behaves.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// Deliberately readable rather than opaque. Every part is a property, so a script can take a preset,
/// look at what it is made of, and change one thing — which is the difference between a preset that
/// teaches the medium and one that merely hides it.
/// </para>
/// </remarks>
public class BrushPreset
{
    #region Constructors
    public BrushPreset(string name, string color, float lineWidth, string lineCap,
        SKShader? grain = null, SKPathEffect? texture = null, SKMaskFilter? edge = null)
    {
        Name = name;
        Color = color;
        LineWidth = lineWidth;
        LineCap = lineCap;
        Grain = grain;
        Texture = texture;
        Edge = edge;
    }
    #endregion

    #region Properties
    /// <summary>The medium this preset imitates, e.g. <c>"pencil"</c>.</summary>
    public string Name { get; }

    /// <summary>The mark's colour, used when <see cref="Grain"/> is absent.</summary>
    public string Color { get; }

    /// <summary>Stroke width the medium is designed around.</summary>
    public float LineWidth { get; }

    /// <summary>Cap style: <c>"round"</c>, <c>"butt"</c> or <c>"square"</c>.</summary>
    public string LineCap { get; }

    /// <summary>What the mark is made of, when the medium deposits unevenly. Null for a solid medium.</summary>
    public SKShader? Grain { get; }

    /// <summary>What happens to the path itself — jitter, stamping. Null for a clean path.</summary>
    public SKPathEffect? Texture { get; }

    /// <summary>What happens at the mark's edge. Null for a hard edge.</summary>
    public SKMaskFilter? Edge { get; }
    #endregion
}

/// <summary>The <c>Skia.Brush</c> sub-namespace: drawing media assembled from the paint primitives.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// Nothing here is new capability — every preset is a shader, a path effect and a mask filter that a
/// script could already assemble. They exist because the assembly is not discoverable: the parts are
/// documented in three separate places, the combinations that work are narrow, and the naive ones
/// fail in both directions. Multiplying a stroke's alpha by noise luminance fades it to nothing;
/// tinting noise through a colour filter flattens it to a solid line. A studio agent with all the
/// primitives available drew every line at uniform width with a hard edge, which is what a missing
/// preset layer looks like.
/// </para>
/// </remarks>
public class SkiaBrushApi
{
    #region Constants
    /// <summary>
    /// Colours the mark with <c>ink</c> and cuts it away where the noise runs dark.
    /// </summary>
    /// <remarks>
    /// The contrast curve is the point. Multiplying alpha by luminance directly gives a uniformly
    /// faint stroke, because turbulence averages low — it dims rather than speckles.
    /// <c>smoothstep</c> across a narrow window turns the same noise into granular deposits with
    /// gaps, which is what a dry medium leaves on paper.
    /// </remarks>
    private const string GrainShader = """
        uniform shader noise;
        uniform float4 ink;
        uniform float lo;
        uniform float hi;
        half4 main(float2 p) {
            half4 n = noise.eval(p);
            float lum = (n.r + n.g + n.b) / 3.0;
            float a = smoothstep(lo, hi, lum);
            return half4(half3(ink.rgb) * a, a);
        }
        """;
    #endregion

    #region Methods
    /// <summary>
    /// Graphite: granular deposit, a wandering line, and no hard edge.
    /// </summary>
    /// <param name="color">Graphite colour.</param>
    /// <param name="width">Stroke width.</param>
    /// <param name="grain">0 for an even deposit, 1 for the default granularity, higher for a drier pencil.</param>
    /// <param name="seed">Changes the grain pattern without changing its character.</param>
    public BrushPreset Pencil(string color = "#3a3a3a", float width = 2.2f, float grain = 1f, int seed = 7) =>
        new("pencil", color, width, "round",
            grain: Grain(color, grain, frequency: 0.45f, octaves: 3, seed: seed, lo: 0.02f, hi: 0.15f),
            texture: grain > 0f ? SKPathEffect.CreateDiscrete(9f, 1.0f, (uint)seed) : null,
            edge: SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 0.6f));

    /// <summary>
    /// Pen and ink: solid, crisp, with just enough irregularity not to read as a vector line.
    /// </summary>
    /// <remarks>
    /// No grain and no softened edge — ink is opaque and its edge is its virtue. The slight path
    /// jitter is what separates a drawn contour from a plotted one; set <paramref name="steadiness"/>
    /// to 1 for a mechanically exact line.
    /// </remarks>
    /// <param name="color">Ink colour.</param>
    /// <param name="width">Stroke width. The three-tier hierarchy is roughly 4, 2 and 1.</param>
    /// <param name="steadiness">0 shakes, 1 is exact.</param>
    public BrushPreset Ink(string color = "#0a0a0c", float width = 2.6f, float steadiness = 0.75f)
    {
        var wobble = Math.Clamp(1f - steadiness, 0f, 1f) * 1.4f;

        return new BrushPreset("ink", color, width, "round",
            texture: wobble > 0.01f ? SKPathEffect.CreateDiscrete(14f, wobble, 3) : null);
    }

    /// <summary>
    /// Chalk or charcoal: coarse deposit, wide, soft-edged, and it sits on the tooth of the paper.
    /// </summary>
    public BrushPreset Chalk(string color = "#2e2a26", float width = 7f, float grain = 1f, int seed = 3) =>
        new("chalk", color, width, "round",
            grain: Grain(color, grain, frequency: 0.18f, octaves: 2, seed: seed, lo: 0.03f, hi: 0.18f),
            edge: SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.4f));

    /// <summary>
    /// Marker: flat, opaque, wide, with the faintly bled edge of ink on absorbent paper.
    /// </summary>
    public BrushPreset Marker(string color = "#1c2530", float width = 6f) =>
        new("marker", color, width, "square",
            edge: SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 0.5f));

    /// <summary>
    /// Separate deposits along the path: stipple, dotted texture, a trail of marks.
    /// </summary>
    /// <remarks>
    /// The advance must exceed the mark's own width or the stamps overlap back into a solid band —
    /// which is the single easiest way to use <c>Skia.PathEffect.stamp</c> and see no effect at all.
    /// The default spacing is three times the dot for that reason.
    /// </remarks>
    /// <param name="color">Mark colour.</param>
    /// <param name="size">Diameter of each deposit.</param>
    /// <param name="spacing">Distance between deposits. Defaults to three times <paramref name="size"/>.</param>
    /// <param name="seed">Changes the jitter without changing its character.</param>
    public BrushPreset Stipple(string color = "#3a3a3a", float size = 2f, float spacing = 0f, int seed = 11)
    {
        if (size <= 0f) throw new ArgumentOutOfRangeException(nameof(size), size, "size must be greater than zero.");

        var advance = spacing > 0f ? spacing : size * 3f;
        var r = size / 2f;

        using var dot = new SKPath();
        dot.AddCircle(0, 0, r);

        var stamp = SKPathEffect.Create1DPath(dot, advance, 0f, SKPath1DPathEffectStyle.Rotate);
        var jitter = SKPathEffect.CreateDiscrete(advance * 1.5f, size * 0.4f, (uint)seed);

        return new BrushPreset("stipple", color, 1f, "round",
            texture: SKPathEffect.CreateCompose(stamp, jitter));
    }

    /// <summary>Builds the grain shader for a dry medium, or null when an even deposit was asked for.</summary>
    private static SKShader? Grain(string color, float amount, float frequency, int octaves, int seed, float lo, float hi)
    {
        if (amount <= 0f) return null;

        var ink = SKColor.Parse(color);

        // A wider window keeps more of the stroke, so less grain; a narrower one breaks it up more.
        var span = (hi - lo) / Math.Clamp(amount, 0.05f, 4f);
        var mid = (lo + hi) / 2f;

        using var effect = SKRuntimeEffect.CreateShader(GrainShader, out var errors);
        if (effect is null || !string.IsNullOrEmpty(errors))
        {
            throw new InvalidOperationException($"The grain shader failed to compile: {errors}");
        }

        var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["ink"] = new[] { ink.Red / 255f, ink.Green / 255f, ink.Blue / 255f, 1f },
            ["lo"] = mid - span / 2f,
            ["hi"] = mid + span / 2f,
        };

        var children = new SKRuntimeEffectChildren(effect)
        {
            ["noise"] = SKShader.CreatePerlinNoiseTurbulence(frequency, frequency, octaves, seed),
        };

        return effect.ToShader(uniforms, children);
    }
    #endregion
}
