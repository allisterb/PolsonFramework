namespace Polson.Animation;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using SkiaSharp;

/// <summary>A 2D affine map, <c>x' = A·x + C·y + E</c>, <c>y' = B·x + D·y + F</c>, in double precision.</summary>
/// <remarks>Composed as Synfig composes its matrices: <c>(m · n)(p) = m(n(p))</c>.</remarks>
internal readonly record struct MotionAffine(double A, double B, double C, double D, double E, double F)
{
    #region Properties
    public static MotionAffine Identity => new(1, 0, 0, 1, 0, 0);

    public double Determinant => A * D - B * C;
    #endregion

    #region Methods
    public static MotionAffine Translate(double x, double y) => new(1, 0, 0, 1, x, y);

    public static MotionAffine Scale(double x, double y) => new(x, 0, 0, y, 0, 0);

    /// <summary>A rotation by degrees: the x axis turns toward the y axis, clockwise on screen when y is down.</summary>
    public static MotionAffine Rotate(double degrees)
    {
        var r = degrees * Math.PI / 180;
        var (s, c) = Math.SinCos(r);
        return new(c, s, -s, c, 0, 0);
    }

    public static MotionAffine operator *(MotionAffine m, MotionAffine n) => new(
        m.A * n.A + m.C * n.B, m.B * n.A + m.D * n.B,
        m.A * n.C + m.C * n.D, m.B * n.C + m.D * n.D,
        m.A * n.E + m.C * n.F + m.E, m.B * n.E + m.D * n.F + m.F);

    public static MotionAffine operator *(MotionAffine m, double k) => new(m.A * k, m.B * k, m.C * k, m.D * k, m.E * k, m.F * k);

    public static MotionAffine operator +(MotionAffine m, MotionAffine n) =>
        new(m.A + n.A, m.B + n.B, m.C + n.C, m.D + n.D, m.E + n.E, m.F + n.F);

    public (double X, double Y) Apply(double x, double y) => (A * x + C * y + E, B * x + D * y + F);

    /// <summary>A direction, without the translation.</summary>
    public (double X, double Y) ApplyVector(double x, double y) => (A * x + C * y, B * x + D * y);

    public MotionAffine Invert()
    {
        var det = Determinant;
        if (Math.Abs(det) < 1e-12) throw new InvalidOperationException("A bone's rest pose has collapsed to a line and cannot be inverted: is a scale zero?");
        var (a, b, c, d) = (D / det, -B / det, -C / det, A / det);
        return new(a, b, c, d, -(a * E + c * F), -(b * E + d * F));
    }

    public SKMatrix ToSk() => new((float)A, (float)C, (float)E, (float)B, (float)D, (float)F, 0, 0, 1);

    /// <summary>
    /// Synfig's decomposition of a matrix into a transformation: offset, the x axis's angle, the y axis's
    /// skew from perpendicular, and the two axis lengths.
    /// </summary>
    public (double OffsetX, double OffsetY, double Angle, double Skew, double ScaleX, double ScaleY) Decompose()
    {
        var angle = Math.Atan2(B, A) * 180 / Math.PI;
        var skew = Math.Atan2(D, C) * 180 / Math.PI - angle - 90;
        while (skew <= -180) skew += 360;
        while (skew > 180) skew -= 360;
        return (E, F, angle, skew, Math.Sqrt(A * A + B * B), Math.Sqrt(C * C + D * D));
    }
    #endregion
}

/// <summary>
/// One bone of a skeleton: what <c>composition.bone(...)</c> returns.
/// </summary>
/// <remarks>
/// <b>Ported from Synfig</b> (<c>bone.cpp</c>, <c>valuenodes/valuenode_bone.cpp</c>, GPL-2-or-later). A bone's
/// frame is <c>parent · translate(origin.x × parent.scalelx, origin.y) · rotate(angle) · scale(scalex, 1)</c>,
/// a root bone's parent being the composition itself; <c>scalelx</c> stretches the bone and moves its
/// children without scaling them, <c>scalex</c> scales the bone and everything below it. A bone is made
/// either from its rest-pose joints (<c>from</c>, <c>to</c>, keyed by <c>turn</c> and <c>stretch</c>) or
/// from Synfig's own fields. Its <b>rest pose</b> — what bound layers and points follow it from — is
/// <c>turn</c> 0 and <c>stretch</c> 1, or Synfig's fields at time 0.
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase
/// spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>. The camelCase
/// form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </para>
/// </remarks>
public sealed class MotionBone
{
    #region Constructors
    internal MotionBone(object? options, int index, IReadOnlyCollection<MotionBone> skeleton)
    {
        var o = new MotionOptions(options, "bone",
            "name", "parent", "from", "to", "turn", "stretch",
            "origin", "angle", "length", "scalelx", "scalex", "width", "tipwidth");

        Name = o.Has("name") ? o.Raw("name")?.ToString() ?? "" : $"bone{index + 1}";
        Parent = o.Has("parent")
            ? o.Raw("parent") as MotionBone ?? throw new ArgumentException($"bone '{Name}'s parent is a bone made by composition.bone(...).")
            : null;
        if (Parent is not null && !skeleton.Contains(Parent))
            throw new ArgumentException($"bone '{Name}'s parent '{Parent.Name}' belongs to another composition.");

        var joints = o.Has("from") || o.Has("to");
        var native = new[] { "origin", "angle", "length", "scalelx", "scalex" }.Where(o.Has).ToArray();
        if (joints && native.Length > 0)
            throw new ArgumentException(
                $"bone '{Name}' is given by its joints (from, to) or by Synfig's fields, not both: drop {string.Join(", ", native)}.");

        var restScalelx = 1d;
        if (joints)
        {
            if (!o.Has("from") || !o.Has("to")) throw new ArgumentException($"bone '{Name}' needs both from and to.");
            var from = MotionTypes.Read(o.Raw("from"), MotionType.Vector, $"bone '{Name}'s from");
            var to = MotionTypes.Read(o.Raw("to"), MotionType.Vector, $"bone '{Name}'s to");

            // Into the parent's rest frame, where a child's origin x is measured against the parent's stretch.
            var inverse = Parent is null ? MotionAffine.Identity : Parent.RestFrame.Invert();
            var (lx, ly) = inverse.Apply(from[0], from[1]);
            var (dx, dy) = inverse.ApplyVector(to[0] - from[0], to[1] - from[1]);
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-9) throw new ArgumentException($"bone '{Name}' has no length: from and to are the same point.");

            var restAngle = Math.Atan2(dy, dx) * 180 / Math.PI;
            Origin = new MotionConstant(MotionType.Vector, [lx / (Parent?.RestScalelx ?? 1), ly]);
            Angle = o.Has("turn")
                ? new MotionAdd(MotionType.Angle, new MotionConstant(MotionType.Angle, [restAngle]),
                    MotionNodeFactory.Node(o.Raw("turn"), MotionType.Angle, $"bone '{Name}'s turn"),
                    new MotionConstant(MotionType.Real, [1]))
                : new MotionConstant(MotionType.Angle, [restAngle]);
            Length = new MotionConstant(MotionType.Real, [length]);
            Scalelx = o.Node("stretch", MotionType.Real, 1d);
            Scalex = new MotionConstant(MotionType.Real, [1]);
            RestAngle = restAngle;
        }
        else
        {
            foreach (var key in new[] { "turn", "stretch" }.Where(o.Has))
                throw new ArgumentException($"bone '{Name}'s {key} goes with from and to; with Synfig's fields, key angle or scalelx instead.");
            Origin = o.Node("origin", MotionType.Vector, new[] { 0d, 0d });
            Angle = o.Node("angle", MotionType.Angle, 0d);
            Length = o.Node("length", MotionType.Real, 100d);
            Scalelx = o.Node("scalelx", MotionType.Real, 1d);
            Scalex = o.Node("scalex", MotionType.Real, 1d);
            RestAngle = Angle.Evaluate(0)[0];
            restScalelx = Scalelx.Evaluate(0)[0];
        }

        var len = Length.Evaluate(0)[0];
        Width = o.Node("width", MotionType.Real, Math.Abs(len) * 0.12);
        Tipwidth = o.Node("tipwidth", MotionType.Real, Math.Abs(len) * 0.06);

        RestScalelx = restScalelx;
        RestFrame = Chain(Parent?.RestFrame, Parent?.RestScalelx ?? 1, Origin.Evaluate(0), RestAngle, Scalex.Evaluate(0)[0]);
        RestLink = RestFrame * MotionAffine.Scale(RestScalelx, 1);
    }
    #endregion

    #region Properties
    public string Name { get; }

    /// <summary>The bone this one hangs from, or null for a root bone.</summary>
    public MotionBone? Parent { get; }

    /// <summary>The bone at rest: <c>{ origin, tip, angle, length }</c> in composition coordinates.</summary>
    public object Rest => Describe(RestLink, Length.Evaluate(0)[0]);

    internal MotionNode Origin { get; }

    internal MotionNode Angle { get; }

    internal MotionNode Length { get; }

    internal MotionNode Scalelx { get; }

    internal MotionNode Scalex { get; }

    internal MotionNode Width { get; }

    internal MotionNode Tipwidth { get; }

    internal double RestAngle { get; }

    internal double RestScalelx { get; }

    /// <summary>The rest pose's frame, without the bone's own stretch: where its children are placed from.</summary>
    internal MotionAffine RestFrame { get; }

    /// <summary>The rest pose's frame with the bone's stretch: what bound values are measured in.</summary>
    internal MotionAffine RestLink { get; }

    internal IEnumerable<MotionNode> Nodes => [Origin, Angle, Length, Scalelx, Scalex, Width, Tipwidth];
    #endregion

    #region Methods
    /// <summary>The bone at a time: <c>{ origin, tip, angle, length }</c> in composition coordinates, the angle in degrees.</summary>
    public object At(double time) => Describe(Link(time), Length.Evaluate(time)[0]);

    /// <summary>Synfig's animated matrix: the bone's frame at a time, without its own stretch.</summary>
    internal MotionAffine Frame(double time) =>
        Chain(Parent?.Frame(time), Parent is null ? 1 : Parent.Scalelx.Evaluate(time)[0],
            Origin.Evaluate(time), Angle.Evaluate(time)[0], Scalex.Evaluate(time)[0]);

    /// <summary>The frame a bone link applies: the animated matrix times the local stretch.</summary>
    internal MotionAffine Link(double time) => Frame(time) * MotionAffine.Scale(Scalelx.Evaluate(time)[0], 1);

    private static MotionAffine Chain(MotionAffine? parent, double parentScalelx, double[] origin, double angle, double scalex) =>
        (parent is { } p ? p * MotionAffine.Translate(origin[0] * parentScalelx, origin[1]) : MotionAffine.Translate(origin[0], origin[1]))
        * MotionAffine.Rotate(angle) * MotionAffine.Scale(scalex, 1);

    private static Dictionary<string, object?> Describe(MotionAffine link, double length)
    {
        var (ox, oy) = link.Apply(0, 0);
        var (tx, ty) = link.Apply(length, 0);
        return new()
        {
            ["origin"] = new Dictionary<string, object?> { ["x"] = ox, ["y"] = oy },
            ["tip"] = new Dictionary<string, object?> { ["x"] = tx, ["y"] = ty },
            ["angle"] = Math.Atan2(link.B, link.A) * 180 / Math.PI,
            ["length"] = Math.Sqrt((tx - ox) * (tx - ox) + (ty - oy) * (ty - oy)),
        };
    }

    /// <summary>
    /// Synfig's skeleton-layer shape for the bone at a time: a capsule from a circle of <c>width</c> at
    /// the origin to one of <c>tipwidth</c> at the tip. A fixed segment count keeps the vertex count the
    /// same at every time, so an SVG path can interpolate.
    /// </summary>
    internal List<SKPoint> Capsule(double time, int? fixedSegments = null)
    {
        const double precision = 0.000000001;
        var m = Frame(time);
        var (ox, oy) = m.Apply(0, 0);
        var (vx, vy) = m.ApplyVector(1, 0);
        var norm = Math.Sqrt(vx * vx + vy * vy);
        (vx, vy) = norm > 0 ? (vx / norm, vy / norm) : (1, 0);
        var length = Length.Evaluate(time)[0] * Scalelx.Evaluate(time)[0];
        if (length < 0) (length, vx, vy) = (-length, -vx, -vy);

        var (p1x, p1y) = (ox + vx * length, oy + vy * length);
        var r0 = Math.Abs(Width.Evaluate(time)[0]);
        var r1 = Math.Abs(Tipwidth.Evaluate(time)[0]);
        var direction = Math.Atan2(vy, vx);
        var angle0 = length - precision > Math.Abs(r1 - r0) ? Math.Acos((r0 - r1) / length) : (r0 > r1 ? 0 : Math.PI);
        var angle1 = Math.PI - angle0;

        var step = 2 * Math.PI / 64;
        var n0 = fixedSegments ?? Math.Max(1, (int)Math.Round(2 * angle1 / step));
        var n1 = fixedSegments ?? Math.Max(1, (int)Math.Round(2 * angle0 / step));
        var points = new List<SKPoint>(n0 + n1 + 2);
        var a = direction + angle0;
        for (var j = 0; ; a += 2 * angle1 / n0)
        {
            points.Add(new((float)(r0 * Math.Cos(a) + ox), (float)(r0 * Math.Sin(a) + oy)));
            if (j++ >= n0) break;
        }

        for (var j = 0; ; a += 2 * angle0 / n1)
        {
            points.Add(new((float)(r1 * Math.Cos(a) + p1x), (float)(r1 * Math.Sin(a) + p1y)));
            if (j++ >= n1) break;
        }

        return points;
    }
    #endregion
}

/// <summary>
/// How a value follows bones: one bone (Synfig's bone link) or several by weight (its bone influence).
/// The value is measured in the rest pose and carried by the bones' frames from there.
/// </summary>
internal abstract class MotionBinding
{
    #region Properties
    /// <summary>The frame at rest, which a bound value is measured against.</summary>
    public abstract MotionAffine Rest { get; }

    public abstract IEnumerable<MotionBone> Bones { get; }
    #endregion

    #region Methods
    /// <summary>The frame at a time.</summary>
    public abstract MotionAffine At(double time);

    /// <summary>Writes a value, given in the bones' rest frame, as the node that carries it.</summary>
    public abstract XElement Wrap(SifWriter sif, string type, XElement local);

    /// <summary>A bone, or a list of <c>[bone, weight]</c> or <c>{ bone, weight }</c>.</summary>
    public static MotionBinding Parse(object? value, string who)
    {
        if (value is MotionBone bone) return new Link(bone);
        if (value is IList list && list.Count > 0)
        {
            var pairs = list.Cast<object?>().Select((item, i) =>
            {
                var at = $"{who}'s weight {i}";
                if (JsInterop.AsDict(item) is { } d)
                    return (d["bone"] as MotionBone ?? throw new ArgumentException($"{at} has no bone."),
                        MotionTypes.Read(d["weight"] ?? 1d, MotionType.Real, $"{at}'s weight")[0]);
                if (item is IList p && p.Count == 2 && p[0] is MotionBone b)
                    return (b, MotionTypes.Read(p[1], MotionType.Real, $"{at}'s weight")[0]);
                throw new ArgumentException($"{at} is [bone, weight] or {{ bone, weight }}.");
            }).ToArray();
            if (Math.Abs(pairs.Sum(p => p.Item2)) < 1e-9) throw new ArgumentException($"{who}'s weights add up to nothing.");
            return new Influence(pairs);
        }

        throw new ArgumentException($"{who} is a bone, or a list of [bone, weight] pairs.");
    }

    protected static XElement BoneRef(SifWriter sif, MotionBone bone) =>
        new("bone_valuenode", new XAttribute("type", "bone_object"), new XAttribute("guid", sif.Bone(bone)));
    #endregion

    #region Types
    private sealed class Link(MotionBone bone) : MotionBinding
    {
        public override MotionAffine Rest => bone.RestLink;

        public override IEnumerable<MotionBone> Bones => [bone];

        public override MotionAffine At(double time) => bone.Link(time);

        public override XElement Wrap(SifWriter sif, string type, XElement local) => new("bone_link", new XAttribute("type", type),
            new XElement("bone", BoneRef(sif, bone)),
            new XElement("base_value", local),
            new[] { "translate", "rotate", "skew", "scale_x", "scale_y" }.Select(f => new XElement(f, SifWriter.Bool(true))));
    }

    /// <summary>Synfig's bone influence: the bones' frames averaged by weight, which is linear blend skinning.</summary>
    private sealed class Influence((MotionBone Bone, double Weight)[] pairs) : MotionBinding
    {
        public override MotionAffine Rest => Blend(b => b.RestLink);

        public override IEnumerable<MotionBone> Bones => pairs.Select(p => p.Bone);

        public override MotionAffine At(double time) => Blend(b => b.Link(time));

        private MotionAffine Blend(Func<MotionBone, MotionAffine> frame)
        {
            var total = pairs.Sum(p => p.Weight);
            var sum = new MotionAffine(0, 0, 0, 0, 0, 0);
            foreach (var (bone, weight) in pairs) sum += frame(bone) * weight;
            return sum * (1 / total);
        }

        public override XElement Wrap(SifWriter sif, string type, XElement local) => new("boneinfluence", new XAttribute("type", type),
            new XElement("bone_weight_list", new XElement("static_list", new XAttribute("type", "bone_weight_pair"),
                pairs.Select(p => new XElement("entry", new XElement("boneweightpair", new XAttribute("type", "bone_weight_pair"),
                    new XElement("bone", BoneRef(sif, p.Bone)),
                    new XElement("weight", new XElement("real", new XAttribute("value", MotionTypes.Format(p.Weight))))))))),
            new XElement("link", local));
    }
    #endregion
}

/// <summary>A point carried by bones from where it sits in the rest pose: <c>Motion.nodes.boneLink</c> and <c>boneInfluence</c>.</summary>
/// <remarks>
/// Synfig's bone link and bone influence on a vector. The point is fixed in the rest pose and converted
/// once into the bones' frame, as Synfig Studio does when a vertex is linked. Exposed to the JavaScript
/// sandbox. See <see cref="MotionNode"/>.
/// </remarks>
public sealed class MotionBoneNode : MotionNode
{
    #region Constructors
    internal MotionBoneNode(MotionBinding binding, double[] world) : base(MotionType.Vector)
    {
        this.binding = binding;
        var (x, y) = binding.Rest.Invert().Apply(world[0], world[1]);
        local = [x, y];
    }
    #endregion

    #region Fields
    private readonly MotionBinding binding;
    private readonly double[] local;
    #endregion

    #region Methods
    internal override double[] Evaluate(double time)
    {
        var (x, y) = binding.At(time).Apply(local[0], local[1]);
        return [x, y];
    }

    internal override XElement ToSif(SifWriter sif) => binding.Wrap(sif, "vector", SifWriter.Value(MotionType.Vector, local));
    #endregion
}
