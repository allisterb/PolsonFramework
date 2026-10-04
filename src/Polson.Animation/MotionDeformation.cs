namespace Polson.Animation;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Polson.Drawing.Skia;

using SkiaSharp;

/// <summary>
/// Synfig's skeleton deformation layer: everything below it in its stack, bent by the bones.
/// </summary>
/// <remarks>
/// <b>Ported from Synfig</b> (<c>layers/layer_skeletondeformation.cpp</c> and <c>layer_meshtransform.cpp</c>,
/// GPL-2-or-later). A grid of <c>xSubdivisions × ySubdivisions</c> cells spans <c>point1</c> to <c>point2</c>.
/// Each bone gives each grid point a weight of <c>percent / distance²</c> — how deep the point sits in the
/// bone's rest capsule, widened by two grid diagonals, over its squared distance from the bone — and a
/// position, the point carried by the similarity from the bone's rest segment to its posed one. A point
/// moves to the weighted average. The stack below, cut to the union of the rest capsules, is drawn through
/// the bent grid, and only cells whose four corners some bone reaches are drawn. A bone's width is
/// therefore its reach: the artwork it bends must lie within it.
/// <para>
/// The result replaces the stack below at full <c>amount</c> (Synfig's straight blend) and fades to it at less.
/// </para>
/// </remarks>
internal sealed class MotionDeformationLayer : MotionLayer
{
    #region Constructors
    public MotionDeformationLayer(MotionOptions o, IReadOnlyList<MotionBone> all) : base(o)
    {
        if (!Blend.IsComposite)
            throw new ArgumentException($"{o.Who} replaces the stack below it, as Synfig's straight blend does; it takes amount, not blend.");
        bones = o.Has("bones")
            ? (o.Raw("bones") as IList ?? throw new ArgumentException($"{o.Who}'s bones is a list of bones."))
                .Cast<object?>().Select((b, i) => b as MotionBone ?? throw new ArgumentException($"{o.Who}'s bones[{i}] is not a bone.")).ToArray()
            : all;
        if (bones.Count == 0) throw new ArgumentException($"{o.Who} has no bones to bend by: make some with composition.bone(...) first.");
        foreach (var b in bones.Where(b => !all.Contains(b)))
            throw new ArgumentException($"{o.Who}'s bone '{b.Name}' belongs to another composition.");

        xCount = Math.Max(1, (int)o.Number("xSubdivisions", 32)) + 1;
        yCount = Math.Max(1, (int)o.Number("ySubdivisions", 32)) + 1;
        if (xCount * yCount > ushort.MaxValue) throw new ArgumentException($"{o.Who}'s grid of {xCount - 1}×{yCount - 1} cells is too fine.");

        // The rest capsules' bounds, padded, unless the corners are given.
        var rest = bones.Select(b => b.RestShape).ToArray();
        var pad = rest.Max(s => Math.Max(s.R0, s.R1));
        var minX = rest.Min(s => Math.Min(s.P0x - s.R0, s.P1x - s.R1)) - pad;
        var minY = rest.Min(s => Math.Min(s.P0y - s.R0, s.P1y - s.R1)) - pad;
        var maxX = rest.Max(s => Math.Max(s.P0x + s.R0, s.P1x + s.R1)) + pad;
        var maxY = rest.Max(s => Math.Max(s.P0y + s.R0, s.P1y + s.R1)) + pad;
        point1 = o.Has("point1") ? MotionTypes.Read(o.Raw("point1"), MotionType.Vector, $"{o.Who}'s point1") : [minX, minY];
        point2 = o.Has("point2") ? MotionTypes.Read(o.Raw("point2"), MotionType.Vector, $"{o.Who}'s point2") : [maxX, maxY];
    }
    #endregion

    #region Fields
    private readonly IReadOnlyList<MotionBone> bones;
    private readonly double[] point1, point2;
    private readonly int xCount, yCount;
    #endregion

    #region Properties
    public IReadOnlyList<MotionBone> Bones => bones;
    #endregion

    #region Methods
    public override IEnumerable<MotionNode> Nodes() => [Amount];

    /// <summary>Synfig's straight blend, which is what replacing the stack below is.</summary>
    protected override int SifBlendMethod => 1;

    protected override void Draw(SkiaCanvas surface, double time, double amount) =>
        throw new InvalidOperationException("A skeleton deformation is applied by its stack, to what is below it.");

    /// <summary>
    /// Bends <paramref name="below"/> — the stack so far, in device pixels, drawn under <paramref name="matrix"/> —
    /// and returns the result in its place.
    /// </summary>
    public SkiaCanvas Apply(SkiaCanvas below, SKMatrix matrix, double time)
    {
        var amount = AmountAt(time);
        var result = new SkiaCanvas(below.Width, below.Height, below.SkBitmap.ColorType);
        result.SkCanvas.Clear(SKColors.Transparent);
        if (amount <= 0)
        {
            result.SkCanvas.DrawBitmap(below.SkBitmap, 0, 0);
            return result;
        }

        // The stack below, cut to the union of the rest capsules (Synfig's mask).
        using var masked = new SkiaCanvas(below.Width, below.Height, below.SkBitmap.ColorType);
        masked.SkCanvas.Clear(SKColors.Transparent);
        using (var mask = new SKPath { FillType = SKPathFillType.Winding })
        {
            foreach (var b in bones) mask.AddPoly([.. b.RestShape.Capsule()], close: true);
            masked.SkCanvas.Save();
            masked.SkCanvas.SetMatrix(matrix);
            masked.SkCanvas.ClipPath(mask, antialias: true);
            masked.SkCanvas.ResetMatrix();
            masked.SkCanvas.DrawBitmap(below.SkBitmap, 0, 0);
            masked.SkCanvas.Restore();
        }

        // Drawn through the bent grid: texture coordinates are where each point was, positions where it went.
        var (positions, texture, indices) = Mesh(time);
        using var warped = new SkiaCanvas(below.Width, below.Height, below.SkBitmap.ColorType);
        warped.SkCanvas.Clear(SKColors.Transparent);
        if (indices.Length > 0 && matrix.TryInvert(out var inverse))
        {
            using var image = SKImage.FromBitmap(masked.SkBitmap);
            using var shader = image.ToShader(SKShaderTileMode.Decal, SKShaderTileMode.Decal,
                new SKSamplingOptions(SKFilterMode.Linear), inverse);
            using var paint = new SKPaint { Shader = shader };
            using var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, positions, texture, null, indices);
            warped.SkCanvas.SetMatrix(matrix);
            warped.SkCanvas.DrawVertices(vertices, SKBlendMode.Dst, paint);
        }

        // Straight blend: the bent stack at amount, the stack as it was at the rest.
        if (amount >= 1)
        {
            result.SkCanvas.DrawBitmap(warped.SkBitmap, 0, 0);
            return result;
        }

        using var fade = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round((1 - amount) * 255)) };
        result.SkCanvas.DrawBitmap(below.SkBitmap, 0, 0, fade);
        using var add = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(amount * 255)), BlendMode = SKBlendMode.Plus };
        result.SkCanvas.DrawBitmap(warped.SkBitmap, 0, 0, add);
        return result;
    }

    /// <summary>Synfig's <c>prepare_mesh</c>: the grid, each point's weighted average position, and the cells every corner of which is reached.</summary>
    internal (SKPoint[] Positions, SKPoint[] Texture, ushort[] Indices) Mesh(double time)
    {
        const double precision = 1e-10;
        var stepX = (point2[0] - point1[0]) / (xCount - 1);
        var stepY = (point2[1] - point1[1]) / (yCount - 1);
        var diagonal = Math.Sqrt(stepX * stepX + stepY * stepY);

        var count = xCount * yCount;
        var sumX = new double[count];
        var sumY = new double[count];
        var weight = new double[count];
        var used = new bool[count];

        foreach (var bone in bones)
        {
            var rest = bone.RestShape;
            var posed = bone.Shape(time);
            var expanded = rest.Expanded(2 * diagonal);
            MotionAffine map;
            try { map = posed.Frame * rest.Frame.Invert(); }
            catch (InvalidOperationException) { continue; }   // a bone of no length bends nothing

            for (var j = 0; j < yCount; j++)
            {
                for (var i = 0; i < xCount; i++)
                {
                    var (x, y) = (point1[0] + i * stepX, point1[1] + j * stepY);
                    var percent = expanded.CenterPercent(x, y);
                    if (percent <= precision) continue;
                    var distance = Math.Max(precision, rest.DistanceToLine(x, y));
                    var w = percent / (distance * distance);
                    var (mx, my) = map.Apply(x, y);
                    var k = j * xCount + i;
                    sumX[k] += mx * w;
                    sumY[k] += my * w;
                    weight[k] += w;
                    used[k] = true;
                }
            }
        }

        var positions = new SKPoint[count];
        var texture = new SKPoint[count];
        for (var j = 0; j < yCount; j++)
        {
            for (var i = 0; i < xCount; i++)
            {
                var k = j * xCount + i;
                var (x, y) = (point1[0] + i * stepX, point1[1] + j * stepY);
                texture[k] = new SKPoint((float)x, (float)y);
                positions[k] = weight[k] > precision ? new SKPoint((float)(sumX[k] / weight[k]), (float)(sumY[k] / weight[k])) : texture[k];
            }
        }

        var indices = new List<ushort>();
        for (var j = 1; j < yCount; j++)
        {
            for (var i = 1; i < xCount; i++)
            {
                var v0 = (j - 1) * xCount + (i - 1);
                var v1 = (j - 1) * xCount + i;
                var v2 = j * xCount + i;
                var v3 = j * xCount + (i - 1);
                if (!(used[v0] && used[v1] && used[v2] && used[v3])) continue;
                indices.AddRange([(ushort)v0, (ushort)v1, (ushort)v3, (ushort)v1, (ushort)v2, (ushort)v3]);
            }
        }

        return (positions, texture, [.. indices]);
    }

    /// <summary>The layer with each bone paired to its rest copy, which the writer adds to the bones section.</summary>
    public override XElement ToSif(SifWriter sif) => Element("skeleton_deformation", "0.2", sif,
        SifWriter.Param("bones", new XElement("static_list", new XAttribute("type", "pair_bone_object_bone_object"),
            bones.Select(b => new XElement("entry", new XElement("composite", new XAttribute("type", "pair_bone_object_bone_object"),
                new XElement("first", new XElement("bone", new XAttribute("type", "bone_object"), new XAttribute("guid", sif.RestBone(b)))),
                new XElement("second", new XElement("bone", new XAttribute("type", "bone_object"), new XAttribute("guid", sif.Bone(b))))))))),
        SifWriter.Param("point1", SifWriter.Value(MotionType.Vector, point1)),
        SifWriter.Param("point2", SifWriter.Value(MotionType.Vector, point2)),
        SifWriter.Param("x_subdivisions", new XElement("integer", new XAttribute("value", xCount - 1))),
        SifWriter.Param("y_subdivisions", new XElement("integer", new XAttribute("value", yCount - 1))));

    public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map) => throw new InvalidOperationException(
        $"The skeleton deformation{(Desc is null ? "" : $" '{Desc}'")} cannot be written as animated SVG: SVG has no mesh warp. "
        + "Capture the composition as frames instead.");
    #endregion
}
