namespace Polson.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SkiaSharp;

/// <summary>
/// A recorded clip flattened onto the page: what <c>Character.track(clip)</c> returns, and what
/// <c>composition.rigFromDrawing(..., { follow })</c> plays.
/// </summary>
/// <remarks>
/// <para>
/// The clip is played on a 3D body and each body part's direction is read as the page sees it from the front.
/// A drawn rig turned to those directions does what the performer did, as far as the page can show it: a part
/// pointing into the page has no direction there, so <c>inPlane</c> says how much of each part's length lies in
/// the page, and the rig holds a part to its parent where little does.
/// </para>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase spelling onto
/// them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>. The camelCase form is the one documented in
/// <c>docs/Polson.core.md</c> and the studio manuals.
/// </para>
/// </remarks>
public sealed class CharacterTrack : IPlanarMotion
{
    #region Constructors
    private CharacterTrack(string clip, double fps, double[] times, Dictionary<string, (double Angle, double InPlane)>[] frames, (double X, double Y)[] roots)
    {
        Clip = clip;
        Fps = fps;
        this.times = times;
        this.frames = frames;
        this.roots = roots;
    }
    #endregion

    #region Fields
    /// <summary>The parts a track carries, in <c>Character.jointMap</c>'s names; <c>hips</c> is the hip line.</summary>
    internal static readonly string[] Parts =
    [
        "hips", "spine", "head",
        "leftUpperArm", "leftForearm", "leftHand", "rightUpperArm", "rightForearm", "rightHand",
        "leftThigh", "leftShin", "leftFoot", "rightThigh", "rightShin", "rightFoot",
    ];

    private readonly double[] times;
    private readonly Dictionary<string, (double Angle, double InPlane)>[] frames;
    private readonly (double X, double Y)[] roots;
    #endregion

    #region Properties
    /// <summary>The clip's name.</summary>
    public string Clip { get; }

    /// <summary>Samples per second.</summary>
    public double Fps { get; }

    /// <summary>How many samples.</summary>
    public int Frames => times.Length;

    /// <summary>Seconds from the first sample to the last.</summary>
    public double Duration => times[^1];

    /// <summary>The sample times, in seconds from the first.</summary>
    public double[] Times => [.. times];

    /// <summary>Per part, the least share of its length in the page plane over the clip, 0 to 1.</summary>
    public Dictionary<string, object?> InPlane => Parts.Where(p => frames[0].ContainsKey(p))
        .ToDictionary(p => p, p => (object?)Math.Round(frames.Min(f => f[p].InPlane), 3));

    /// <summary>The parts that leave the page plane, in words. Empty when the whole motion is in it.</summary>
    public string[] Warnings
    {
        get
        {
            var warnings = new List<string>();
            foreach (var p in Parts.Where(p => frames[0].ContainsKey(p) && p != "hips"))
            {
                double lo = frames.Min(f => f[p].InPlane), hi = frames.Max(f => f[p].InPlane);
                if (hi < 0.5)
                    warnings.Add($"{p} points out of the page throughout ({hi:P0} at most); a drawn rig holds it to its parent.");
                else if (lo < 0.5)
                    warnings.Add($"{p} turns out of the page (as little as {lo:P0} of it in the page); a drawn rig holds it toward its parent there.");
            }

            return [.. warnings];
        }
    }
    #endregion

    #region Methods
    /// <summary>One sample, to look at: <c>{ time, parts: { name: { angleDeg, inPlane } }, root: { x, y } }</c>.</summary>
    public Dictionary<string, object?> At(int frame)
    {
        if (frame < 0 || frame >= times.Length)
            throw new ArgumentOutOfRangeException(nameof(frame), $"The track has {times.Length} samples, 0 to {times.Length - 1}; got {frame}.");
        return new()
        {
            ["time"] = Math.Round(times[frame], 4),
            ["parts"] = frames[frame].ToDictionary(kv => kv.Key, kv => (object?)new Dictionary<string, object?>
            {
                ["angleDeg"] = Math.Round(kv.Value.Angle, 3),
                ["inPlane"] = Math.Round(kv.Value.InPlane, 3),
            }),
            ["root"] = new Dictionary<string, object?> { ["x"] = Math.Round(roots[frame].X, 4), ["y"] = Math.Round(roots[frame].Y, 4) },
        };
    }

    double[] IPlanarMotion.FrameTimes => times;

    bool IPlanarMotion.TryGetDirection(string part, int frame, out double angleDeg, out double inPlane)
    {
        var found = frames[frame].TryGetValue(part, out var d);
        (angleDeg, inPlane) = found ? d : (0d, 0d);
        return found;
    }

    (double X, double Y) IPlanarMotion.RootOffset(int frame) => roots[frame];

    /// <summary>Samples <paramref name="clip"/> on <paramref name="body"/> from <paramref name="from"/> to <paramref name="to"/> seconds.</summary>
    internal static CharacterTrack Sample(FaceMesh body, PoseRetarget.Clip clip, double fps, double from, double to, float yawDeg)
    {
        if (body.Rig is not { } rig || rig.Aliases.Count == 0)
            throw new ArgumentException("Character.track needs a character with body-part names: Character.stock() or Character.load(name).");
        var missing = new[] { "hips", "leftUpperArm", "rightUpperArm", "leftForearm", "rightForearm", "leftHand", "rightHand",
            "leftThigh", "rightThigh", "leftShin", "rightShin", "leftFoot", "rightFoot" }.Where(p => !rig.Aliases.ContainsKey(p)).ToArray();
        if (missing.Length > 0)
            throw new ArgumentException($"Character.track: this character has no {string.Join(", ", missing)}.");

        var view = MeshToolkit.Pose.From(new Dictionary<string, object?> { ["yawDeg"] = yawDeg }, body);
        string H(string part) => rig.Aliases[part];
        string[] Children(string handle) => [.. rig.JointParent.Where(kv => kv.Value == handle).Select(kv => kv.Key)];

        // Page coordinates (y down) and depth for every joint under a pose.
        Dictionary<string, Vector3> Project(object? pose) => CharacterIk.Joints(rig, pose, "track").ToDictionary(kv => kv.Key, kv =>
        {
            var r = view.Rotate(new SKPoint3(kv.Value.X, kv.Value.Y, kv.Value.Z));
            return new Vector3(r.X, -r.Y, r.Z);
        }, StringComparer.Ordinal);

        Vector3? Tip(Dictionary<string, Vector3> at, string part)
        {
            var kids = Children(H(part)).Where(at.ContainsKey).ToArray();
            return kids.Length == 0 ? null : kids.Aggregate(Vector3.Zero, (s, k) => s + at[k]) / kids.Length;
        }

        Dictionary<string, (Vector3 From, Vector3 To)> Segments(Dictionary<string, Vector3> at)
        {
            var hipMid = (at[H("leftThigh")] + at[H("rightThigh")]) / 2;
            var shoulderMid = (at[H("leftUpperArm")] + at[H("rightUpperArm")]) / 2;
            var s = new Dictionary<string, (Vector3, Vector3)>(StringComparer.Ordinal)
            {
                ["hips"] = (at[H("rightThigh")], at[H("leftThigh")]),
                ["spine"] = (hipMid, shoulderMid),
            };
            if (rig.Aliases.ContainsKey("head") && Tip(at, "head") is { } crown) s["head"] = (shoulderMid, crown);
            foreach (var side in new[] { "left", "right" })
            {
                s[side + "UpperArm"] = (at[H(side + "UpperArm")], at[H(side + "Forearm")]);
                s[side + "Forearm"] = (at[H(side + "Forearm")], at[H(side + "Hand")]);
                if (Tip(at, side + "Hand") is { } hand) s[side + "Hand"] = (at[H(side + "Hand")], hand);
                s[side + "Thigh"] = (at[H(side + "Thigh")], at[H(side + "Shin")]);
                s[side + "Shin"] = (at[H(side + "Shin")], at[H(side + "Foot")]);
                if (Tip(at, side + "Foot") is { } toe) s[side + "Foot"] = (at[H(side + "Foot")], toe);
            }

            return s;
        }

        var bind = Project(null);
        var legs = new[] { "left", "right" }.Average(side =>
            (bind[H(side + "Shin")] - bind[H(side + "Thigh")]).Length() + (bind[H(side + "Foot")] - bind[H(side + "Shin")]).Length());
        var bindHips = (bind[H("leftThigh")] + bind[H("rightThigh")]) / 2;

        var count = Math.Max(1, (int)Math.Round((to - from) * fps)) + 1;
        var times = new double[count];
        var frames = new Dictionary<string, (double, double)>[count];
        var roots = new (double, double)[count];
        for (var i = 0; i < count; i++)
        {
            var t = count == 1 ? from : from + ((to - from) * i / (count - 1));
            var at = Project(PoseRetarget.Retarget(rig, clip, (float)t));
            times[i] = t - from;
            frames[i] = Segments(at).ToDictionary(kv => kv.Key, kv =>
            {
                var d = kv.Value.To - kv.Value.From;
                var flat = Math.Sqrt((d.X * d.X) + (d.Y * d.Y));
                var full = d.Length();
                return (Math.Atan2(d.Y, d.X) * 180 / Math.PI, full < 1e-6 ? 0d : flat / full);
            }, StringComparer.Ordinal);
            var hips = (at[H("leftThigh")] + at[H("rightThigh")]) / 2;
            roots[i] = ((hips.X - bindHips.X) / legs, (hips.Y - bindHips.Y) / legs);
        }

        return new CharacterTrack(clip.Name, fps, times, frames, roots);
    }
    #endregion
}
