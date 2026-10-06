namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
    #region Figure Canons
    /// <summary>
    /// Where a figure's landmarks fall, as fractions of its standing height down from the crown, and how
    /// wide it is in its own head units against the adult male: the trunk by <c>Width</c> and the
    /// shoulder, rib, waist and hip factors on it, the limbs by <c>Arms</c> and <c>Legs</c> on the head.
    /// </summary>
    readonly record struct FigureCanon(float Chin, float Nipples, float Navel, float Crotch, float Knee, float Wrist,
        float Width, float Shoulders, float Ribs, float Waist, float Hips, float Arms, float Legs, float HeadWidth)
    {
        static readonly float[] AdultMale = [0f, 0.125f, 0.25f, 0.375f, 0.5f, 0.75f, 1f];

        /// <summary>A fraction of the adult male's height to the same landmark on this figure.</summary>
        public float Remap(float f)
        {
            float[] to = [0f, Chin, Nipples, Navel, Crotch, Knee, 1f];
            if (f <= 0f) return f * Chin / 0.125f;
            for (var i = 1; i < AdultMale.Length; i++)
                if (f <= AdultMale[i])
                    return to[i - 1] + (to[i] - to[i - 1]) * (f - AdultMale[i - 1]) / (AdultMale[i] - AdultMale[i - 1]);
            return 1f + (f - 1f);
        }

        public static FigureCanon Lerp(FigureCanon a, FigureCanon b, float t)
        {
            float L(float x, float y) => x + (y - x) * t;
            return new(L(a.Chin, b.Chin), L(a.Nipples, b.Nipples), L(a.Navel, b.Navel), L(a.Crotch, b.Crotch),
                L(a.Knee, b.Knee), L(a.Wrist, b.Wrist), L(a.Width, b.Width), L(a.Shoulders, b.Shoulders),
                L(a.Ribs, b.Ribs), L(a.Waist, b.Waist), L(a.Hips, b.Hips), L(a.Arms, b.Arms), L(a.Legs, b.Legs),
                L(a.HeadWidth, b.HeadWidth));
        }
    }

    // Loomis, Figure Drawing for All It's Worth, p. 26: chin 1, nipples 2, navel 3, crotch 4, the bottom
    // of the knees 6, in eight heads; the wrist level with the crotch.
    static readonly FigureCanon MaleCanon = new(0.125f, 0.25f, 0.375f, 0.5f, 0.75f, 0.5f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0.72f);

    // Loomis p. 27: still eight heads, the nipples and navel a sixth of a head lower than the male's and
    // the crotch a third of a head below the middle, the wrists level with it. Two heads at the widest
    // against his two and a third, a waist of one head, and thighs a little wider than the armpits.
    // The width factors are read off that plate and are the studio's.
    static readonly FigureCanon FemaleCanon = new(0.125f, 13f / 48f, 19f / 48f, 13f / 24f, 0.75f, 13f / 24f,
        1f, 0.86f, 0.9f, 0.9f, 1.1f, 0.9f, 1f, 0.72f);

    // Loomis p. 29, "Ideal Proportions at Various Ages": 4 heads at one year, 5 at three, 6 at five, 7 at
    // ten, 7 1/2 at fifteen. The landmarks were measured off the plate's own head lines; the trunk widths
    // are each figure's widest against the adult's 2 1/3 heads, and the head widths off its row of heads.
    // Limbs are in head units, not narrowed with the trunk: a one-year-old's thigh is about as thick for
    // its head as an adult's, and thins through childhood as the baby fat goes (Gautier, 1,001 Figures
    // p. 113). Measured by the studio, so good to a few hundredths of the height; the limbs are judged.
    static readonly (float Age, FigureCanon Canon)[] ChildCanons =
    [
        (1f,  new(0.25f,   0.40f,  0.52f,  0.71f,  0.84f, 0.61f, 0.62f, 1f, 1f, 1f, 1f, 0.95f, 1.00f, 0.86f)),
        (3f,  new(0.20f,   0.33f,  0.46f,  0.59f,  0.78f, 0.61f, 0.69f, 1f, 1f, 1f, 1f, 0.90f, 0.95f, 0.81f)),
        (5f,  new(1f / 6f, 0.30f,  0.41f,  0.57f,  0.76f, 0.60f, 0.72f, 1f, 1f, 1f, 1f, 0.85f, 0.88f, 0.77f)),
        (10f, new(1f / 7f, 0.28f,  0.39f,  0.53f,  0.73f, 0.56f, 0.80f, 1f, 1f, 1f, 1f, 0.82f, 0.85f, 0.76f)),
        (15f, new(2f / 15f, 0.265f, 0.382f, 0.515f, 0.74f, 0.53f, 0.90f, 1f, 1f, 1f, 1f, 0.90f, 0.92f, 0.73f))
    ];

    /// <summary>The build and age a figure was asked for, and the canon they give.</summary>
    static (string Build, float? Age, FigureCanon Canon) ReadFigureCanon(IDictionary? opt)
    {
        var build = opt?["build"]?.ToString()?.Trim().ToLowerInvariant() ?? "male";
        var adult = build switch
        {
            "male" => MaleCanon,
            "female" => FemaleCanon,
            _ => throw new ArgumentException(
                $"createMannequinFigure build '{build}' not recognised. Accepted: male, female. A child is an age: {{ age: 5 }}.")
        };
        if (opt == null || !opt.Contains("age") || opt["age"] == null) return (build, null, adult);

        var age = Num(opt, "age", 18f);
        if (!float.IsFinite(age) || age < 0f)
            throw new ArgumentException($"createMannequinFigure age is years, 0 or more; got {opt["age"]}.");

        // Under a year is drawn as one: Gautier gives a newborn four heads, as Loomis gives the one-year-old.
        // From fifteen the figure grows into the adult build by eighteen.
        if (age <= ChildCanons[0].Age) return (build, age, ChildCanons[0].Canon);
        if (age >= 18f) return (build, age, adult);
        for (var i = 1; i < ChildCanons.Length; i++)
            if (age <= ChildCanons[i].Age)
            {
                var (a0, c0) = ChildCanons[i - 1];
                var (a1, c1) = ChildCanons[i];
                return (build, age, FigureCanon.Lerp(c0, c1, (age - a0) / (a1 - a0)));
            }
        var last = ChildCanons[^1];
        return (build, age, FigureCanon.Lerp(last.Canon, adult, (age - last.Age) / (18f - last.Age)));
    }

    static readonly string[] MannequinOptionKeys = ["shoulderTiltDeg", "pelvicTiltDeg", "spineOffset", "shoulderSpanHeads", "build", "age", "pose"];
    static readonly string[] PoseKeys = ["spineDeg", "neckDeg", "lineOfAction", "leftArm", "rightArm", "leftLeg", "rightLeg"];
    static readonly string[] ArmKeys = ["shoulderDeg", "elbowDeg"];
    static readonly string[] LegKeys = ["hipDeg", "kneeDeg"];
    const string ElbowConvention = "shoulderDeg aims the upper arm on the page (0 right, 90 down, negative up); elbowDeg swings the forearm from the line of the upper arm, signed, so 0 is a straight arm.";
    const string KneeConvention = "hipDeg aims the thigh on the page (0 right, 90 down); kneeDeg swings the shin from the line of the thigh, signed, so 0 is a straight leg.";

    /// <summary>
    /// Refuses a pose key the figure does not read, by name. Exact case, because the keys are read exactly: a
    /// misspelled or invented one (<c>headTurnDeg</c>, <c>elbowDegg</c>) was accepted and did nothing, so the
    /// figure quietly was not the pose the script described.
    /// </summary>
    static void RefuseUnknownPoseKeys(IDictionary? dict, string where, string[] accepted, string? hint)
    {
        if (dict is null) return;
        var unknown = dict.Keys.Cast<object?>().Select(k => k?.ToString()).Where(k => k is not null && !accepted.Contains(k, StringComparer.Ordinal)).ToList();
        if (unknown.Count == 0) return;
        var near = unknown.Select(u => accepted.FirstOrDefault(a => string.Equals(a, u, StringComparison.OrdinalIgnoreCase)))
                          .Where(a => a is not null).ToList();
        throw new ArgumentException(
            $"{where} has no {(unknown.Count > 1 ? "keys" : "key")} {string.Join(", ", unknown.Select(u => $"'{u}'"))}. It takes {string.Join(", ", accepted)}."
            + (near.Count > 0 ? $" Did you mean {string.Join(", ", near.Select(n => $"'{n}'"))}? Keys are case-sensitive." : "")
            + (hint is null ? "" : " " + hint));
    }
    #endregion

    #region Posing
    /// <summary>Length of a limb segment, so a pose keeps the proportions the canon gave it.</summary>
    static float SegmentLength(Point2D from, Point2D to) =>
        MathF.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y));

    /// <summary>Screen angle of a segment in degrees, measured the way <see cref="AlongSegment"/> reads them.</summary>
    static float SegmentAngle(Point2D from, Point2D to) =>
        MathF.Atan2(to.Y - from.Y, to.X - from.X) * 180f / MathF.PI;

    /// <summary>The point <paramref name="length"/> away from <paramref name="from"/> at a screen angle.</summary>
    /// <remarks>
    /// Screen degrees, so <b>90 is straight down</b> — the direction gravity and a hanging arm both
    /// go. Chosen over the mathematical convention because every default in this toolkit is a
    /// standing figure, and an author reasoning about a pose is reasoning about the drawing.
    /// </remarks>
    static Point2D AlongSegment(Point2D from, float length, float degrees)
    {
        var r = degrees * MathF.PI / 180f;
        return new Point2D(from.X + MathF.Cos(r) * length, from.Y + MathF.Sin(r) * length);
    }

    /// <summary>Rotates a point about a pivot, for leaning the upper body over the pelvis.</summary>
    /// <summary>The direction from <paramref name="from"/> to <paramref name="to"/>, in degrees from upright, positive toward screen right.</summary>
    static float Lean(Point2D from, Point2D to) => MathF.Atan2(to.X - from.X, from.Y - to.Y) * 180f / MathF.PI;

    /// <summary>
    /// Where the crown lands with no lean: the head turned and the two bends applied as the figure applies
    /// them, to the standing figure's points. A lean then rotates it about the pelvis.
    /// </summary>
    static Point2D Crown(Point2D navel, Point2D neck, Point2D head, float neckDeg, float waistDeg, float neckBendDeg, float H)
    {
        head = RotateAbout(head, neck, neckDeg);
        head = RotateAbout(head, navel, waistDeg);
        neck = RotateAbout(neck, navel, waistDeg);
        head = RotateAbout(head, neck, neckBendDeg);
        return RotateAbout(new Point2D(head.X, head.Y - H * 0.50f), head, neckDeg + waistDeg + neckBendDeg);
    }

    static Point2D RotateAbout(Point2D point, Point2D pivot, float degrees)
    {
        if (degrees == 0f) return point;
        var r = degrees * MathF.PI / 180f;
        float cos = MathF.Cos(r), sin = MathF.Sin(r), dx = point.X - pivot.X, dy = point.Y - pivot.Y;
        return new Point2D(pivot.X + dx * cos - dy * sin, pivot.Y + dx * sin + dy * cos);
    }

    static float? PoseAngle(IDictionary? pose, string key) =>
        pose != null && pose.Contains(key) && pose[key] != null
            ? Convert.ToSingle(pose[key], CultureInfo.InvariantCulture)
            : null;

    /// <summary>
    /// Re-places one three-segment limb from joint angles, keeping the segment lengths the
    /// proportional construction produced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Forward kinematics, and deliberately the simplest kind: the <paramref name="rootKey"/> angle is
    /// <b>absolute</b> on screen, the <paramref name="bendKey"/> angle is <b>relative</b> to the segment
    /// above it — which is what a joint actually is. An elbow does not have an opinion about the
    /// horizon; it has an opinion about the upper arm.
    /// </para>
    /// <para>
    /// <b>Every omitted angle falls back to the default figure's own value</b>, recovered from the
    /// points the canon already computed. So a limb with no pose is bit-identical to the unposed
    /// figure, and a limb given only <c>elbowDeg</c> keeps the standing shoulder. That is what makes
    /// this additive rather than a second construction to keep in step with the first.
    /// </para>
    /// <para>
    /// The hand and foot keep their own offset from the segment before them, so they swing with the
    /// forearm and shin rather than staying pinned downward.
    /// </para>
    /// </remarks>
    static (Point2D Mid, Point2D End, Point2D Tip) PoseLimb(
        IDictionary? pose, string rootKey, string bendKey,
        Point2D anchor, Point2D defaultMid, Point2D defaultEnd, Point2D defaultTip)
    {
        if (pose == null) return (defaultMid, defaultEnd, defaultTip);

        var upper = SegmentAngle(anchor, defaultMid);
        var lower = SegmentAngle(defaultMid, defaultEnd);
        var tipOffset = SegmentAngle(defaultEnd, defaultTip) - lower;

        var rootDeg = PoseAngle(pose, rootKey) ?? upper;
        var bendDeg = PoseAngle(pose, bendKey) ?? lower - upper;

        var mid = AlongSegment(anchor, SegmentLength(anchor, defaultMid), rootDeg);
        var end = AlongSegment(mid, SegmentLength(defaultMid, defaultEnd), rootDeg + bendDeg);
        var tip = AlongSegment(end, SegmentLength(defaultEnd, defaultTip), rootDeg + bendDeg + tipOffset);
        return (mid, end, tip);
    }

    /// <summary>
    /// Reads <c>pose.lineOfAction</c>, refusing anything it cannot honour by name.
    /// </summary>
    /// <remarks>
    /// Past 120 degrees of turning the two bends fold the torso through itself, which renders as a
    /// figure and reads as a defect, so it is refused rather than drawn.
    /// </remarks>
    static (string Shape, float Amount, float? Lean)? ReadLineOfAction(IDictionary? pose)
    {
        if (pose == null || !pose.Contains("lineOfAction") || pose["lineOfAction"] == null) return null;
        var line = JsInterop.AsDict(pose["lineOfAction"])
            ?? throw new ArgumentException("pose.lineOfAction takes an object, such as { shape: 'C', turnDeg: 24 }.");

        foreach (var key in line.Keys)
            if (key?.ToString() is not ("shape" or "turnDeg" or "leanDeg"))
                throw new ArgumentException(
                    $"pose.lineOfAction has no option '{key}'. It takes shape ('C' or 'S'), turnDeg (degrees the line turns, end to end) and leanDeg (where the head is from the pelvis).");

        var shape = (line.Contains("shape") ? line["shape"]?.ToString() : null)?.Trim().ToUpperInvariant() ?? "C";
        if (shape is not ("C" or "S"))
            throw new ArgumentException(
                $"pose.lineOfAction.shape '{line["shape"]}' is not a shape this can bend. Use 'C' (one curve, both bends the same way) or 'S' (the neck bends against the waist).");

        var amount = Num(line, "turnDeg", 0f);
        if (!float.IsFinite(amount))
            throw new ArgumentException("pose.lineOfAction.turnDeg must be a finite number of degrees.");
        if (MathF.Abs(amount) > 120f)
            throw new ArgumentException(
                $"pose.lineOfAction.turnDeg {amount} is more turning than a torso has: past 120 degrees the waist and neck fold the figure through itself. Pose the legs for the rest of the curve.");

        float? lean = null;
        if (line.Contains("leanDeg"))
        {
            lean = Num(line, "leanDeg", 0f);
            if (!float.IsFinite(lean.Value) || MathF.Abs(lean.Value) > 90f)
                throw new ArgumentException($"pose.lineOfAction.leanDeg is degrees from upright, -90 to 90; got {line["leanDeg"]}.");
            if (Num(pose, "spineDeg", 0f) != 0f)
                throw new ArgumentException(
                    "pose.lineOfAction.leanDeg and pose.spineDeg both set the lean. leanDeg says where the head ends up and solves the spine for it; give one or the other.");
        }

        return (shape, amount, lean);
    }

    /// <summary>A smooth curve through the points, as SVG path data: Catmull-Rom as cubic Béziers.</summary>
    static string SmoothPathData(IReadOnlyList<Point2D> pts)
    {
        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder($"M{F(pts[0].X)},{F(pts[0].Y)}");
        for (var i = 0; i < pts.Count - 1; i++)
        {
            Point2D p0 = pts[Math.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.Min(i + 2, pts.Count - 1)];
            sb.Append(CultureInfo.InvariantCulture,
                $" C{F(p1.X + (p2.X - p0.X) / 6f)},{F(p1.Y + (p2.Y - p0.Y) / 6f)} {F(p2.X - (p3.X - p1.X) / 6f)},{F(p2.Y - (p3.Y - p1.Y) / 6f)} {F(p2.X)},{F(p2.Y)}");
        }
        return sb.ToString();
    }

    /// <summary>Largest distance of the interior points off the chord between the first and last.</summary>
    static float Swing(IReadOnlyList<Point2D> pts)
    {
        Point2D a = pts[0], b = pts[^1];
        float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return 0f;
        var most = 0f;
        for (var i = 1; i < pts.Count - 1; i++)
            most = MathF.Max(most, MathF.Abs(dx * (a.Y - pts[i].Y) - (a.X - pts[i].X) * dy) / len);
        return most;
    }
    #endregion

    #region Mannequin
    public Dictionary<string, object?> CreateMannequinFigure(float originX, float originY, float totalHeight = 560f, object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        var (build, age, canon) = ReadFigureCanon(opt);

        // Every station below is written in the adult male's head units, 0 at the crown and 8 at the
        // floor, and placed through the canon: Y(s) carries a station to where this build and age put
        // it, by its landmarks. For the adult male Y(s) is originY + H * s exactly, so a figure drawn
        // before builds existed is unchanged. H is the figure's own head; W is how wide it is, in the
        // same units: an infant's head is a quarter of its height and its body much narrower than an
        // adult's measured in it.
        var H = totalHeight * canon.Chin;
        var W = H * canon.Width;
        float Y(float s) => originY + totalHeight * canon.Remap(s / 8f);
        float Span(float a, float b) => (Y(b) - Y(a)) / (H * (b - a));
        var shoulderTiltDeg = opt != null && opt.Contains("shoulderTiltDeg") ? Convert.ToSingle(opt["shoulderTiltDeg"], CultureInfo.InvariantCulture) : -6f;
        var pelvicTiltDeg = opt != null && opt.Contains("pelvicTiltDeg") ? Convert.ToSingle(opt["pelvicTiltDeg"], CultureInfo.InvariantCulture) : 6f;
        var spineOffset = opt != null && opt.Contains("spineOffset") ? Convert.ToSingle(opt["spineOffset"], CultureInfo.InvariantCulture) : 6f;

        var radShoulder = (shoulderTiltDeg * MathF.PI) / 180f;
        var radPelvis = (pelvicTiltDeg * MathF.PI) / 180f;

        // Head (0.0H to 1.0H)
        var headY = Y(0.5f);
        var headCenter = new Point2D(originX, headY);

        // Neck (1.0H to 1.3H)
        var neckCenter = new Point2D(originX + spineOffset * 0.2f, Y(1.15f));

        // Shoulders / Clavicles (1.4H)
        var shoulderY = Y(1.4f);
        // In head units, so it survives any figure height. The canons disagree and all of them are
        // usable: Loomis gives 2 1/3 H for the figure at its widest and about 2 H for the shoulder
        // "cape"; Faragasso, after Reilly, gives 1 1/3 H from the pit of the neck to each shoulder,
        // so 2 2/3 H across. The default is none of them — it is a shoulder-joint span, which is a
        // narrower measurement again — so an agent following a particular canon has to be able to
        // say so rather than being stuck with ours.
        var shoulderSpanHeads = opt != null && opt.Contains("shoulderSpanHeads")
            ? Convert.ToSingle(opt["shoulderSpanHeads"], CultureInfo.InvariantCulture)
            : 1.8f;
        var shoulderSpan = opt != null && opt.Contains("shoulderSpanHeads") ? H * shoulderSpanHeads : W * shoulderSpanHeads * canon.Shoulders;
        var leftShoulder = new Point2D(originX - MathF.Cos(radShoulder) * (shoulderSpan * 0.5f), shoulderY - MathF.Sin(radShoulder) * (shoulderSpan * 0.5f));
        var rightShoulder = new Point2D(originX + MathF.Cos(radShoulder) * (shoulderSpan * 0.5f), shoulderY + MathF.Sin(radShoulder) * (shoulderSpan * 0.5f));
        var sternalNotch = new Point2D(originX + spineOffset * 0.3f, shoulderY);

        // Ribcage (1.4H to 2.8H)
        var ribcageCenter = new Point2D(originX + spineOffset * 0.6f, Y(2.1f));
        var ribcageRx = W * 0.85f * canon.Ribs;
        var ribcageRy = H * 0.70f * Span(1.4f, 2.8f);

        // Navel (3.0H)
        var navel = new Point2D(originX + spineOffset * 0.8f, Y(3.0f));

        // Pelvis (3.2H to 4.0H)
        var pelvisY = Y(3.6f);
        var pelvisSpan = W * 1.4f * canon.Hips;
        var leftHip = new Point2D(originX - MathF.Cos(radPelvis) * (pelvisSpan * 0.5f), pelvisY - MathF.Sin(radPelvis) * (pelvisSpan * 0.5f));
        var rightHip = new Point2D(originX + MathF.Cos(radPelvis) * (pelvisSpan * 0.5f), pelvisY + MathF.Sin(radPelvis) * (pelvisSpan * 0.5f));
        var pelvisCenter = new Point2D(originX + spineOffset * 0.4f, pelvisY);
        var crotch = new Point2D(originX, Y(4.0f));

        // The arm is placed by where the wrist hangs, not by the body's landmarks: a child's arm is
        // short for its trunk, so at one year the wrist reaches the hip where an adult's reaches the
        // crotch (Loomis, Figure Drawing p. 29). Elbow and hand keep the adult's shares of the arm.
        var wristY = originY + totalHeight * canon.Wrist;
        float Arm(float s) => shoulderY + (wristY - shoulderY) * (s - 1.4f) / 2.6f;

        // Left Arm (Shoulder -> Elbow 2.8H -> Wrist 4.0H -> Hand 4.8H)
        var leftElbow = new Point2D(leftShoulder.X - W * 0.3f, Arm(2.8f));
        var leftWrist = new Point2D(leftShoulder.X - W * 0.2f, wristY);
        var leftHand = new Point2D(leftWrist.X - 2f, Arm(4.75f));

        // Right Arm
        var rightElbow = new Point2D(rightShoulder.X + W * 0.35f, Arm(2.85f));
        var rightWrist = new Point2D(rightShoulder.X + W * 0.25f, wristY);
        var rightHand = new Point2D(rightWrist.X + 2f, Arm(4.75f));

        // Left Leg (Hip -> Knee 6.0H -> Ankle 7.8H -> Foot 8.0H)
        var leftKnee = new Point2D(leftHip.X + W * 0.05f, Y(6.0f));
        var leftAnkle = new Point2D(leftKnee.X - W * 0.05f, Y(7.8f));
        var leftFoot = new Point2D(leftAnkle.X - W * 0.15f, Y(8.0f));

        // Right Leg
        var rightKnee = new Point2D(rightHip.X - W * 0.05f, Y(6.0f));
        var rightAnkle = new Point2D(rightKnee.X + W * 0.05f, Y(7.8f));
        var rightFoot = new Point2D(rightAnkle.X + W * 0.15f, Y(8.0f));

        // A pose re-places the limbs from joint angles. Applied *after* the canon has laid the figure
        // out, so the segment lengths are the canon's and only the directions change — and a limb the
        // pose does not mention is left exactly where the standing figure put it.
        var pose = JsInterop.AsDict(opt != null && opt.Contains("pose") ? opt["pose"] : null);
        RefuseUnknownPoseKeys(opt, "createMannequinFigure options", MannequinOptionKeys, null);
        RefuseUnknownPoseKeys(pose, "pose", PoseKeys,
            "Turn or tilt the head with neckDeg; bend the torso with lineOfAction.");
        var leftArmPose = JsInterop.AsDict(pose != null && pose.Contains("leftArm") ? pose["leftArm"] : null);
        var rightArmPose = JsInterop.AsDict(pose != null && pose.Contains("rightArm") ? pose["rightArm"] : null);
        var leftLegPose = JsInterop.AsDict(pose != null && pose.Contains("leftLeg") ? pose["leftLeg"] : null);
        var rightLegPose = JsInterop.AsDict(pose != null && pose.Contains("rightLeg") ? pose["rightLeg"] : null);
        RefuseUnknownPoseKeys(leftArmPose, "pose.leftArm", ArmKeys, ElbowConvention);
        RefuseUnknownPoseKeys(rightArmPose, "pose.rightArm", ArmKeys, ElbowConvention);
        RefuseUnknownPoseKeys(leftLegPose, "pose.leftLeg", LegKeys, KneeConvention);
        RefuseUnknownPoseKeys(rightLegPose, "pose.rightLeg", LegKeys, KneeConvention);

        // The spine leans the whole upper body over the pelvis — ribcage, shoulders, neck and head,
        // and the arms with them, because an arm hangs from a shoulder that has moved. Applied before
        // the limbs so an arm the pose does not mention still swings with the lean instead of being
        // left behind in the standing position.
        //
        // Positive leans the figure's own left (screen right, toward increasing x at the shoulders).
        // A separate `neckDeg` counter-rotates the head, which is what keeps a leaning figure looking
        // where it was looking rather than tipping its gaze with its chest.
        var spineDeg = PoseAngle(pose, "spineDeg") ?? 0f;
        var neckDeg = PoseAngle(pose, "neckDeg") ?? 0f;

        // The line of action's two bends, found before the lean because a lean is solved against them.
        // The split is not a constant. A polyline approximating one even curve turns at each vertex in
        // proportion to the segments either side of it, so each bend's share comes from the chain's own
        // lengths: pelvis-navel and navel-neck for the waist, navel-neck and neck-head for the neck. The
        // rotations leave lengths alone, so measuring before the lean is the same as after.
        var line = ReadLineOfAction(pose);
        float waistDeg = 0f, neckBendDeg = 0f;
        if (line is { Amount: not 0f } bend)
        {
            var below = SegmentLength(pelvisCenter, navel);
            var middle = SegmentLength(navel, neckCenter);
            var above = SegmentLength(neckCenter, headCenter);
            var waistShare = (below + middle) / (below + 2f * middle + above);
            waistDeg = bend.Amount * waistShare;
            neckBendDeg = bend.Amount * (1f - waistShare) * (bend.Shape == "S" ? -1f : 1f);
        }

        // `leanDeg` names where the head ends up from the pelvis, and leans the whole figure there with the
        // pelvis tilting too, so the curve and the lean are set separately. spineDeg is a different thing: the
        // trunk bending over the pelvis, which the torso reads as a side bend, so a figure leaned right that
        // way stretches its left side however its curve goes. A body leaning into a reach leans from the legs
        // and keeps its own curve, and the agent who drew sketch1's skipper could not get that: a lean one
        // way and a C the other partly cancel. Rotating about the pelvis and then bending is the same as
        // bending and then rotating, so the bent figure's own lean plus the rotation is the lean, exactly.
        // The hips and the crotch turn with it. A leg hangs straight from its hip wherever the hip went, and
        // a posed leg keeps its own angles on the page: where the feet plant is the legs' to say, and turned
        // with the pelvis an unposed leg swings out by the whole rotation, which reads as a fall.
        if (line?.Lean is { } wantLean)
        {
            var bodyLean = wantLean - Lean(pelvisCenter, Crown(navel, neckCenter, headCenter, neckDeg, waistDeg, neckBendDeg, H));
            spineDeg = bodyLean;
            pelvicTiltDeg += bodyLean;
            var (oldLeft, oldRight) = (leftHip, rightHip);
            leftHip = RotateAbout(leftHip, pelvisCenter, bodyLean);
            rightHip = RotateAbout(rightHip, pelvisCenter, bodyLean);
            crotch = RotateAbout(crotch, pelvisCenter, bodyLean);
            Point2D By(Point2D p, Point2D from, Point2D to) => new(p.X + to.X - from.X, p.Y + to.Y - from.Y);
            (leftKnee, leftAnkle, leftFoot) = (By(leftKnee, oldLeft, leftHip), By(leftAnkle, oldLeft, leftHip), By(leftFoot, oldLeft, leftHip));
            (rightKnee, rightAnkle, rightFoot) = (By(rightKnee, oldRight, rightHip), By(rightAnkle, oldRight, rightHip), By(rightFoot, oldRight, rightHip));
        }

        if (spineDeg != 0f || neckDeg != 0f)
        {
            var pivot = pelvisCenter;
            headCenter = RotateAbout(headCenter, pivot, spineDeg);
            neckCenter = RotateAbout(neckCenter, pivot, spineDeg);
            sternalNotch = RotateAbout(sternalNotch, pivot, spineDeg);
            ribcageCenter = RotateAbout(ribcageCenter, pivot, spineDeg);
            navel = RotateAbout(navel, pivot, spineDeg);
            leftShoulder = RotateAbout(leftShoulder, pivot, spineDeg);
            rightShoulder = RotateAbout(rightShoulder, pivot, spineDeg);
            leftElbow = RotateAbout(leftElbow, pivot, spineDeg);
            leftWrist = RotateAbout(leftWrist, pivot, spineDeg);
            leftHand = RotateAbout(leftHand, pivot, spineDeg);
            rightElbow = RotateAbout(rightElbow, pivot, spineDeg);
            rightWrist = RotateAbout(rightWrist, pivot, spineDeg);
            rightHand = RotateAbout(rightHand, pivot, spineDeg);

            // The head turns about the neck, on top of whatever the spine did.
            headCenter = RotateAbout(headCenter, neckCenter, neckDeg);
        }

        // The line of action bends the torso where a torso bends. `spineDeg` rotates everything above
        // the pelvis as one rigid piece, which moves the centre line without curving it (Manual 24 §4
        // measured it flat across five poses). Stanchfield's construction is solid-flexible: head,
        // ribcage and pelvis are solids that keep their shape, and the neck and waist between them are
        // where the bending happens (Drawn to Life, ch. 25-26). So the curve is two bends, one at each
        // flexible part — the waist hinged at the navel, the neck at the neck — and a C bends both the
        // same way while an S bends the neck back against the waist. The two angles were found above.
        if (waistDeg != 0f || neckBendDeg != 0f)
        {
            var waist = navel;
            headCenter = RotateAbout(headCenter, waist, waistDeg);
            neckCenter = RotateAbout(neckCenter, waist, waistDeg);
            sternalNotch = RotateAbout(sternalNotch, waist, waistDeg);
            ribcageCenter = RotateAbout(ribcageCenter, waist, waistDeg);
            leftShoulder = RotateAbout(leftShoulder, waist, waistDeg);
            rightShoulder = RotateAbout(rightShoulder, waist, waistDeg);
            leftElbow = RotateAbout(leftElbow, waist, waistDeg);
            leftWrist = RotateAbout(leftWrist, waist, waistDeg);
            leftHand = RotateAbout(leftHand, waist, waistDeg);
            rightElbow = RotateAbout(rightElbow, waist, waistDeg);
            rightWrist = RotateAbout(rightWrist, waist, waistDeg);
            rightHand = RotateAbout(rightHand, waist, waistDeg);

            headCenter = RotateAbout(headCenter, neckCenter, neckBendDeg);
        }

        (leftElbow, leftWrist, leftHand) = PoseLimb(leftArmPose, "shoulderDeg", "elbowDeg",
            leftShoulder, leftElbow, leftWrist, leftHand);
        (rightElbow, rightWrist, rightHand) = PoseLimb(rightArmPose, "shoulderDeg", "elbowDeg",
            rightShoulder, rightElbow, rightWrist, rightHand);
        (leftKnee, leftAnkle, leftFoot) = PoseLimb(leftLegPose, "hipDeg", "kneeDeg",
            leftHip, leftKnee, leftAnkle, leftFoot);
        (rightKnee, rightAnkle, rightFoot) = PoseLimb(rightLegPose, "hipDeg", "kneeDeg",
            rightHip, rightKnee, rightAnkle, rightFoot);

        // The lean is an orientation, not just a displacement. The masses used to carry only their
        // static tilt while `spineDeg` moved their centres, so a figure leaning 18 degrees kept a
        // perfectly upright head and an untilted ribcage. The pelvis is the pivot and so is unmoved.
        var ribcageTiltDeg = shoulderTiltDeg + spineDeg + waistDeg;
        var headAngleDeg = spineDeg + neckDeg + waistDeg + neckBendDeg;

        // The centre line read back out of the figure, whether or not one was asked for, so any pose
        // can be measured: pelvis, navel, ribcage, neck, head and crown, which is the torso's axis.
        var crown = RotateAbout(new Point2D(headCenter.X, headCenter.Y - H * 0.50f), headCenter, headAngleDeg);
        var axis = new[] { pelvisCenter, navel, ribcageCenter, neckCenter, headCenter, crown };

        var figure = new Dictionary<string, object?>
        {
            ["headUnit"] = H,
            ["totalHeight"] = totalHeight,
            // What the figure was built as, with the widths the geometry draws its masses at.
            ["build"] = new Dictionary<string, object?>
            {
                ["name"] = build,
                ["age"] = age,
                ["heads"] = 1f / canon.Chin,
                ["widthUnit"] = W,
                ["armUnit"] = H * canon.Arms,
                ["legUnit"] = H * canon.Legs,
                ["waistUnit"] = W * canon.Waist
            },
            // Echoed so a caller can read back what the figure is doing — and so a later pass can
            // reproduce or nudge a pose without having kept the arguments that made it.
            ["posed"] = pose != null,
            ["head"] = new Dictionary<string, object?> { ["center"] = ToDict(headCenter), ["rx"] = H * canon.HeadWidth * 0.5f, ["ry"] = H * 0.50f, ["angleDeg"] = headAngleDeg },
            ["neck"] = ToDict(neckCenter),
            ["sternum"] = ToDict(sternalNotch),
            ["clavicles"] = new Dictionary<string, object?> { ["left"] = ToDict(leftShoulder), ["right"] = ToDict(rightShoulder), ["center"] = ToDict(sternalNotch) },
            ["ribcage"] = new Dictionary<string, object?> { ["center"] = ToDict(ribcageCenter), ["rx"] = ribcageRx, ["ry"] = ribcageRy, ["tiltDeg"] = ribcageTiltDeg },
            ["navel"] = ToDict(navel),
            ["pelvis"] = new Dictionary<string, object?> { ["center"] = ToDict(pelvisCenter), ["leftHip"] = ToDict(leftHip), ["rightHip"] = ToDict(rightHip), ["rx"] = W * 0.70f * canon.Hips, ["ry"] = H * 0.45f * Span(3.15f, 4.05f), ["tiltDeg"] = pelvicTiltDeg },
            ["crotch"] = ToDict(crotch),
            ["leftArm"] = new Dictionary<string, object?> { ["shoulder"] = ToDict(leftShoulder), ["elbow"] = ToDict(leftElbow), ["wrist"] = ToDict(leftWrist), ["hand"] = ToDict(leftHand) },
            ["rightArm"] = new Dictionary<string, object?> { ["shoulder"] = ToDict(rightShoulder), ["elbow"] = ToDict(rightElbow), ["wrist"] = ToDict(rightWrist), ["hand"] = ToDict(rightHand) },
            ["leftLeg"] = new Dictionary<string, object?> { ["hip"] = ToDict(leftHip), ["knee"] = ToDict(leftKnee), ["ankle"] = ToDict(leftAnkle), ["foot"] = ToDict(leftFoot) },
            ["rightLeg"] = new Dictionary<string, object?> { ["hip"] = ToDict(rightHip), ["knee"] = ToDict(rightKnee), ["ankle"] = ToDict(rightAnkle), ["foot"] = ToDict(rightFoot) },
            ["lineOfAction"] = new Dictionary<string, object?>
            {
                ["shape"] = line?.Shape,
                ["turnDeg"] = line?.Amount ?? 0f,
                ["waistDeg"] = waistDeg,
                ["neckDeg"] = neckBendDeg,
                // Where the crown is from the pelvis, in degrees from upright, positive toward screen right:
                // the lean, measured however it was set.
                ["leanDeg"] = Lean(pelvisCenter, crown),
                // In head units, so a threshold chosen on a small draft means the same on a final.
                ["swing"] = Swing(axis) / H,
                ["points"] = axis.Select(p => (object?)ToDict(p)).ToList(),
                ["d"] = SmoothPathData(axis)
            }
        };

        // Cheap enough to be unconditional — closed-form over the same masses the geometry uses, no
        // paths and no boolean operations. A posed figure's extent is *not* its height: a thrown arm
        // reaches wider than the canon ever does, so fitting one to a panel by height alone runs a
        // limb straight off the edge.
        figure["bounds"] = FigureBounds(figure);
        return figure;
    }

    public void DrawMannequinWireframe(CanvasRenderingContext2D ctx, object figureObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(figureObj) is not IDictionary fig) return;

        var opt = JsInterop.AsDict(options);
        var blueLine = opt?["blueLineColor"]?.ToString() ?? "#4a90e2";
        var graphite = opt?["graphiteColor"]?.ToString() ?? "#444444";
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1.5f;

        var head = JsInterop.AsDict(fig["head"]);
        var headCenter = ExtractPoint(head?["center"]);
        var headRx = head != null && head.Contains("rx") ? Convert.ToSingle(head["rx"], CultureInfo.InvariantCulture) : 25f;
        // The pose orients the masses as well as placing them; without this a leaning figure keeps
        // a perfectly upright head, which is what it did before these three angles were read.
        var headAngle = Num(head, "angleDeg", 0f) * MathF.PI / 180f;
        var headRy = head != null && head.Contains("ry") ? Convert.ToSingle(head["ry"], CultureInfo.InvariantCulture) : 35f;

        var neck = ExtractPoint(fig["neck"]);
        var sternum = ExtractPoint(fig["sternum"]);
        var navel = ExtractPoint(fig["navel"]);
        var crotch = ExtractPoint(fig["crotch"]);

        var ribcage = JsInterop.AsDict(fig["ribcage"]);
        var ribCenter = ExtractPoint(ribcage?["center"]);
        var ribRx = ribcage != null && ribcage.Contains("rx") ? Convert.ToSingle(ribcage["rx"], CultureInfo.InvariantCulture) : 60f;
        var ribRy = ribcage != null && ribcage.Contains("ry") ? Convert.ToSingle(ribcage["ry"], CultureInfo.InvariantCulture) : 50f;
        var ribTilt = Num(ribcage, "tiltDeg", 0f) * MathF.PI / 180f;

        var pelvis = JsInterop.AsDict(fig["pelvis"]);
        var pelCenter = ExtractPoint(pelvis?["center"]);
        var pelRx = pelvis != null && pelvis.Contains("rx") ? Convert.ToSingle(pelvis["rx"], CultureInfo.InvariantCulture) : 50f;
        var pelRy = pelvis != null && pelvis.Contains("ry") ? Convert.ToSingle(pelvis["ry"], CultureInfo.InvariantCulture) : 32f;
        var pelTilt = Num(pelvis, "tiltDeg", 0f) * MathF.PI / 180f;

        var lArm = JsInterop.AsDict(fig["leftArm"]);
        var rArm = JsInterop.AsDict(fig["rightArm"]);
        var lLeg = JsInterop.AsDict(fig["leftLeg"]);
        var rLeg = JsInterop.AsDict(fig["rightLeg"]);

        ctx.Save();

        // 1. Blue-line Skeleton Gesture & Masses
        ctx.StrokeStyle = blueLine;
        ctx.LineWidth = lineWidth;

        // Head Ellipse
        ctx.BeginPath();
        ctx.Ellipse(headCenter.X, headCenter.Y, headRx, headRy, headAngle, 0f, MathF.PI * 2f);
        ctx.Stroke();

        // Spine Line of Action (Cervical -> Thoracic -> Lumbar -> Sacral)
        ctx.BeginPath();
        ctx.MoveTo(headCenter.X, headCenter.Y + headRy);
        ctx.LineTo(neck.X, neck.Y);
        ctx.QuadraticCurveTo(sternum.X, sternum.Y, ribCenter.X, ribCenter.Y);
        ctx.QuadraticCurveTo(navel.X, navel.Y, pelCenter.X, pelCenter.Y);
        ctx.LineTo(crotch.X, crotch.Y);
        ctx.Stroke();

        // Ribcage Egg
        ctx.BeginPath();
        ctx.Ellipse(ribCenter.X, ribCenter.Y, ribRx, ribRy, ribTilt, 0f, MathF.PI * 2f);
        ctx.Stroke();

        // Pelvic Basin
        ctx.BeginPath();
        ctx.Ellipse(pelCenter.X, pelCenter.Y, pelRx, pelRy, pelTilt, 0f, MathF.PI * 2f);
        ctx.Stroke();

        // 2. Graphite Limb Bones & Joint Hinges
        ctx.StrokeStyle = graphite;
        ctx.LineWidth = lineWidth * 1.25f;

        void DrawLimb(Point2D a, Point2D b, Point2D c, Point2D d)
        {
            ctx.BeginPath();
            ctx.MoveTo(a.X, a.Y);
            ctx.LineTo(b.X, b.Y);
            ctx.LineTo(c.X, c.Y);
            ctx.LineTo(d.X, d.Y);
            ctx.Stroke();

            // Joint hinges
            ctx.BeginPath();
            ctx.Arc(b.X, b.Y, 5f, 0f, MathF.PI * 2f);
            ctx.Arc(c.X, c.Y, 4f, 0f, MathF.PI * 2f);
            ctx.Stroke();
        }

        if (lArm != null) DrawLimb(ExtractPoint(lArm["shoulder"]), ExtractPoint(lArm["elbow"]), ExtractPoint(lArm["wrist"]), ExtractPoint(lArm["hand"]));
        if (rArm != null) DrawLimb(ExtractPoint(rArm["shoulder"]), ExtractPoint(rArm["elbow"]), ExtractPoint(rArm["wrist"]), ExtractPoint(rArm["hand"]));
        if (lLeg != null) DrawLimb(ExtractPoint(lLeg["hip"]), ExtractPoint(lLeg["knee"]), ExtractPoint(lLeg["ankle"]), ExtractPoint(lLeg["foot"]));
        if (rLeg != null) DrawLimb(ExtractPoint(rLeg["hip"]), ExtractPoint(rLeg["knee"]), ExtractPoint(rLeg["ankle"]), ExtractPoint(rLeg["foot"]));

        ctx.Restore();
    }

    public void DrawMannequinSolid(CanvasRenderingContext2D ctx, object figureObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(figureObj) is not IDictionary fig) return;

        var opt = JsInterop.AsDict(options);
        var fillColor = opt?["fillColor"]?.ToString() ?? "#dbe7f2";
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#9bbcd9";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.8f;

        var H = fig.Contains("headUnit") ? Convert.ToSingle(fig["headUnit"], CultureInfo.InvariantCulture) : 70f;

        var buildUnits = JsInterop.AsDict(fig["build"]);
        var armU = Num(buildUnits, "armUnit", H);
        var legU = Num(buildUnits, "legUnit", H);

        ctx.Save();
        ctx.FillStyle = fillColor;
        ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = strokeWidth;

        // 1. Shaded Limbs (Tapered Cylinders)
        void DrawCylinderLimb(Point2D a, Point2D b, float r1, float r2)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < 0.001f) dist = 1f;
            var nx = -dy / dist;
            var ny = dx / dist;

            ctx.BeginPath();
            ctx.MoveTo(a.X - nx * r1, a.Y - ny * r1);
            ctx.LineTo(b.X - nx * r2, b.Y - ny * r2);
            ctx.LineTo(b.X + nx * r2, b.Y + ny * r2);
            ctx.LineTo(a.X + nx * r1, a.Y + ny * r1);
            ctx.ClosePath();
            ctx.Fill();
            ctx.Stroke();
        }

        var lLeg = JsInterop.AsDict(fig["leftLeg"]);
        var rLeg = JsInterop.AsDict(fig["rightLeg"]);
        var lArm = JsInterop.AsDict(fig["leftArm"]);
        var rArm = JsInterop.AsDict(fig["rightArm"]);

        // Legs
        if (lLeg != null)
        {
            DrawCylinderLimb(ExtractPoint(lLeg["hip"]), ExtractPoint(lLeg["knee"]), legU * 0.28f, legU * 0.20f);
            DrawCylinderLimb(ExtractPoint(lLeg["knee"]), ExtractPoint(lLeg["ankle"]), legU * 0.20f, legU * 0.14f);
        }
        if (rLeg != null)
        {
            DrawCylinderLimb(ExtractPoint(rLeg["hip"]), ExtractPoint(rLeg["knee"]), legU * 0.28f, legU * 0.20f);
            DrawCylinderLimb(ExtractPoint(rLeg["knee"]), ExtractPoint(rLeg["ankle"]), legU * 0.20f, legU * 0.14f);
        }

        // Pelvis
        var pelvis = JsInterop.AsDict(fig["pelvis"]);
        var pelCenter = ExtractPoint(pelvis?["center"]);
        var pelRx = pelvis != null && pelvis.Contains("rx") ? Convert.ToSingle(pelvis["rx"], CultureInfo.InvariantCulture) : H * 0.70f;
        var pelRy = pelvis != null && pelvis.Contains("ry") ? Convert.ToSingle(pelvis["ry"], CultureInfo.InvariantCulture) : H * 0.45f;
        var pelTilt = Num(pelvis, "tiltDeg", 0f) * MathF.PI / 180f;
        ctx.BeginPath();
        ctx.Ellipse(pelCenter.X, pelCenter.Y, pelRx, pelRy, pelTilt, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Stroke();

        // Ribcage
        var ribcage = JsInterop.AsDict(fig["ribcage"]);
        var ribCenter = ExtractPoint(ribcage?["center"]);
        var ribRx = ribcage != null && ribcage.Contains("rx") ? Convert.ToSingle(ribcage["rx"], CultureInfo.InvariantCulture) : H * 0.85f;
        var ribRy = ribcage != null && ribcage.Contains("ry") ? Convert.ToSingle(ribcage["ry"], CultureInfo.InvariantCulture) : H * 0.70f;
        var ribTilt = Num(ribcage, "tiltDeg", 0f) * MathF.PI / 180f;
        ctx.BeginPath();
        ctx.Ellipse(ribCenter.X, ribCenter.Y, ribRx, ribRy, ribTilt, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Stroke();

        // Arms
        if (lArm != null)
        {
            DrawCylinderLimb(ExtractPoint(lArm["shoulder"]), ExtractPoint(lArm["elbow"]), armU * 0.22f, armU * 0.16f);
            DrawCylinderLimb(ExtractPoint(lArm["elbow"]), ExtractPoint(lArm["wrist"]), armU * 0.16f, armU * 0.12f);
        }
        if (rArm != null)
        {
            DrawCylinderLimb(ExtractPoint(rArm["shoulder"]), ExtractPoint(rArm["elbow"]), armU * 0.22f, armU * 0.16f);
            DrawCylinderLimb(ExtractPoint(rArm["elbow"]), ExtractPoint(rArm["wrist"]), armU * 0.16f, armU * 0.12f);
        }

        // Head
        var head = JsInterop.AsDict(fig["head"]);
        var headCenter = ExtractPoint(head?["center"]);
        var headRx = head != null && head.Contains("rx") ? Convert.ToSingle(head["rx"], CultureInfo.InvariantCulture) : H * 0.36f;
        var headAngle = Num(head, "angleDeg", 0f) * MathF.PI / 180f;
        var headRy = head != null && head.Contains("ry") ? Convert.ToSingle(head["ry"], CultureInfo.InvariantCulture) : H * 0.50f;
        ctx.BeginPath();
        ctx.Ellipse(headCenter.X, headCenter.Y, headRx, headRy, headAngle, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Stroke();

        ctx.Restore();
    }

    public void DrawTorsoMusculature(CanvasRenderingContext2D ctx, object figureObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(figureObj) is not IDictionary fig) return;

        var opt = JsInterop.AsDict(options);
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#1a2938";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 2.0f;
        var H = fig.Contains("headUnit") ? Convert.ToSingle(fig["headUnit"], CultureInfo.InvariantCulture) : 70f;

        var sternum = ExtractPoint(fig["sternum"]);
        var navel = ExtractPoint(fig["navel"]);
        var neck = ExtractPoint(fig["neck"]);
        var clavicles = JsInterop.AsDict(fig["clavicles"]);
        var leftClav = ExtractPoint(clavicles?["left"]);
        var rightClav = ExtractPoint(clavicles?["right"]);
        var leftArm = JsInterop.AsDict(fig["leftArm"]);
        var rightArm = JsInterop.AsDict(fig["rightArm"]);

        // Hampton's active/passive rule (Figure Drawing: Design and Invention, "Anatomy and Motion"):
        // an active shape squashes, a passive one stretches, and drawing them symmetrically is what
        // kills the gesture. The pose already says which side is which - the shoulder line tilts down
        // toward the closed side - so the compression is read off the figure rather than asked for.
        var shoulderDrop = rightClav.Y - leftClav.Y;
        var tiltSpan = MathF.Max(H * 0.5f, MathF.Abs(rightClav.X - leftClav.X));
        var squash = MathF.Max(-0.35f, MathF.Min(0.35f, shoulderDrop / tiltSpan));
        var leftScale = 1f - squash;       // shoulder down on the right => right side compresses
        var rightScale = 1f + squash;

        ctx.Save();
        ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = strokeWidth;
        ctx.LineCap = "round";

        // 1. Clavicle Handlebars
        ctx.BeginPath();
        ctx.MoveTo(leftClav.X, leftClav.Y);
        ctx.QuadraticCurveTo((leftClav.X + sternum.X) * 0.5f, sternum.Y + 4f, sternum.X, sternum.Y);
        ctx.QuadraticCurveTo((rightClav.X + sternum.X) * 0.5f, sternum.Y + 4f, rightClav.X, rightClav.Y);
        ctx.Stroke();

        // 2. Pectoralis Major Chest Plates
        var pecY = sternum.Y + H * 0.55f;
        var pecW = H * 0.65f;

        // Hampton's pectoral is a fan whose widest part is low, near the nipple, and whose tail wraps to
        // the front of the humerus. The active side keeps less of that width; the passive side spreads.
        void Pectoral(Point2D clav, float dir, float scale)
        {
            ctx.BeginPath();
            ctx.MoveTo(sternum.X, sternum.Y + 8f);
            ctx.LineTo(sternum.X, pecY);
            ctx.QuadraticCurveTo(sternum.X + dir * pecW * 0.5f * scale, pecY + 6f, clav.X - dir * 8f, pecY - 8f);
            ctx.LineTo(clav.X - dir * 4f, clav.Y + 8f);
            ctx.Stroke();
        }

        Pectoral(leftClav, -1f, leftScale);
        Pectoral(rightClav, 1f, rightScale);

        // 3. Linea Alba & Rectus Abdominis Six-Pack
        ctx.BeginPath();
        ctx.MoveTo(sternum.X, pecY);
        ctx.LineTo(navel.X, navel.Y + H * 0.4f);
        ctx.Stroke();

        // Hampton: the abdominal group holds EIGHT sections, and the row at the navel is the straight
        // one - the rows above it progressively rise to a peak. Two flat tiers used to be drawn here,
        // which is a six-pack with no gesture in it at all.
        var abHalf = H * 0.35f;
        for (var i = 0; i < 4; i++)
        {
            var tierY = navel.Y - (navel.Y - pecY) * (i * 0.20f);
            var rise = abHalf * 0.22f * i;                  // the navel row is flat; each row above bows more
            ctx.BeginPath();
            ctx.MoveTo(sternum.X - abHalf * leftScale, tierY);
            ctx.QuadraticCurveTo(sternum.X, tierY - rise * 2f, sternum.X + abHalf * rightScale, tierY);
            ctx.Stroke();
        }

        // 4. Sternocleidomastoid. Hampton's shape is a baseball bat running diagonally from the
        // manubrium to the base of the skull, behind the ear - and it is never drawn symmetrically,
        // because one side is always higher.
        void Sternomastoid(Point2D clav, float dir, float scale)
        {
            var top = new Point2D(neck.X + dir * H * 0.16f, neck.Y - H * 0.10f * scale);
            var foot = new Point2D(sternum.X + dir * H * 0.06f, sternum.Y);
            ctx.BeginPath();
            ctx.MoveTo(top.X, top.Y);
            ctx.QuadraticCurveTo(neck.X + dir * H * 0.10f, (top.Y + foot.Y) * 0.5f, foot.X, foot.Y);
            ctx.Stroke();
        }

        Sternomastoid(leftClav, -1f, leftScale);
        Sternomastoid(rightClav, 1f, rightScale);

        // 5. Deltoid. Hampton's shape is an inverted triangle from the shoulder girdle down to an
        // insertion about halfway along the upper arm; from the front it is the thin version of it.
        void Deltoid(Point2D clav, IDictionary? arm, float dir)
        {
            var shoulder = ExtractPoint(arm?["shoulder"]);
            var elbow = ExtractPoint(arm?["elbow"]);
            var insert = new Point2D(shoulder.X + (elbow.X - shoulder.X) * 0.42f, shoulder.Y + (elbow.Y - shoulder.Y) * 0.42f);

            ctx.BeginPath();
            ctx.MoveTo(clav.X, clav.Y);
            ctx.QuadraticCurveTo(shoulder.X + dir * H * 0.40f, shoulder.Y + H * 0.14f, insert.X, insert.Y);
            ctx.QuadraticCurveTo(shoulder.X + dir * H * 0.03f, (shoulder.Y + insert.Y) * 0.5f, clav.X, clav.Y);
            ctx.Stroke();
        }

        Deltoid(leftClav, leftArm, -1f);
        Deltoid(rightClav, rightArm, 1f);

        ctx.Restore();
    }
    #endregion

    #region Reach
    static readonly string[] ReachOptions = ["to", "bend"];
    static readonly string[] ReachBends = ["down", "up", "left", "right", "out", "in"];

    /// <summary>
    /// The arm angles that put a hand on a point: <c>pose.rightArm = Drawing.reachArm(fig, 'right', p, { to: 'palm' }).pose</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two-joint inverse kinematics, solved exactly, against the figure as built: the shoulder is where the
    /// torso pose put it and an arm's pose does not move it, so rebuilding the figure with the returned
    /// <c>{ shoulderDeg, elbowDeg }</c> lands the point where asked. Solve after the torso pose is final; a
    /// change of lean or curve moves the shoulder and the answer with it.
    /// </para>
    /// <para>
    /// <c>to</c> is the part of the hand that goes on the point: <c>wrist</c> (the default), <c>palm</c>,
    /// halfway along the hand and where a rope or a handle passes through a closed fist, or <c>hand</c>, the
    /// fingertips. The hand keeps its own angle to the forearm, as it does in a pose.
    /// </para>
    /// <para>
    /// Two elbows reach any point, one either side of the line from shoulder to target. <c>bend</c> picks the
    /// side the elbow goes: <c>down</c>, <c>up</c>, <c>left</c> or <c>right</c> on the page, or <c>out</c> and
    /// <c>in</c>, away from or toward the body's centre line. Left out, the elbow goes down, which is where an
    /// elbow hangs, unless the arm is already bent more than 30 degrees, when it keeps that bend's side, so
    /// solving again after a small change does not flip the elbow.
    /// </para>
    /// <para>
    /// A point out of reach is not refused: the arm straightens toward it, <c>reached</c> is false and
    /// <c>miss</c> says how far short it fell, in pixels. So is a point too close to fold onto.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ReachArm(object figureObj, string side, object point, object? options = null)
    {
        var fig = ReachFigure(figureObj, "reachArm");
        var limb = side switch
        {
            "left" or "leftArm" => "leftArm",
            "right" or "rightArm" => "rightArm",
            _ => throw new ArgumentException($"reachArm takes the side 'left' or 'right', and got '{side}'.", nameof(side))
        };
        var (to, bend) = ReachOptionsOf(options, "reachArm", ["wrist", "palm", "hand"], "wrist");
        var share = to switch { "palm" => 0.5f, "hand" => 1f, _ => 0f };
        var r = ReachLimb(fig, limb, ["shoulder", "elbow", "wrist", "hand"], point, share, bend, "down", "reachArm");
        return ReachResult(r, "shoulderDeg", "elbowDeg", "elbow", to);
    }

    /// <summary>
    /// The leg angles that put a foot on a point: <c>pose.leftLeg = Drawing.reachLeg(fig, 'left', p, { to: 'foot' }).pose</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same solver as <see cref="ReachArm"/>, from the hip, and as exact: a leg's pose does not move its hip.
    /// The hip does move with the pelvis, so solve after the torso pose and any <c>leanDeg</c> are final.
    /// </para>
    /// <para>
    /// <c>to</c> is <c>ankle</c> (the default) or <c>foot</c>, the end of the foot, which is what plants a
    /// figure on a deck or a step: put the foot on the surface and the ankle sits above it. The foot keeps its
    /// own angle to the shin, as it does in a pose.
    /// </para>
    /// <para>
    /// <c>bend</c> takes the same sides as an arm. Left out, the knee goes <c>out</c>, away from the body's
    /// centre line, which is how a bent knee reads from the front, unless the leg is already bent more than 30
    /// degrees, when it keeps that side. A figure seen side on bends its knee the way it faces: say so, with
    /// <c>left</c> or <c>right</c>.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ReachLeg(object figureObj, string side, object point, object? options = null)
    {
        var fig = ReachFigure(figureObj, "reachLeg");
        var limb = side switch
        {
            "left" or "leftLeg" => "leftLeg",
            "right" or "rightLeg" => "rightLeg",
            _ => throw new ArgumentException($"reachLeg takes the side 'left' or 'right', and got '{side}'.", nameof(side))
        };
        var (to, bend) = ReachOptionsOf(options, "reachLeg", ["ankle", "foot"], "ankle");
        var r = ReachLimb(fig, limb, ["hip", "knee", "ankle", "foot"], point, to == "foot" ? 1f : 0f, bend, "out", "reachLeg");
        return ReachResult(r, "hipDeg", "kneeDeg", "knee", to);
    }

    static IDictionary ReachFigure(object figureObj, string call) =>
        JsInterop.AsDict(figureObj) is IDictionary fig && fig.Contains("leftArm") && fig.Contains("leftLeg")
            ? fig
            : throw new ArgumentException($"{call} needs a figure from Drawing.createMannequinFigure(...).", nameof(figureObj));

    static (string To, string Bend) ReachOptionsOf(object? options, string call, string[] parts, string defaultPart)
    {
        var opt = JsInterop.AsDict(options);
        string to = defaultPart, bend = "";
        if (opt == null) return (to, bend);
        foreach (var key in opt.Keys)
        {
            var k = key?.ToString();
            var v = opt[key!]?.ToString() ?? "";
            switch (k)
            {
                case "to" when Array.IndexOf(parts, v) >= 0: to = v; break;
                case "to": throw new ArgumentException($"{call}'s to is {string.Join(", ", parts.Select(x => $"'{x}'"))}, and got '{v}'.");
                case "bend" when Array.IndexOf(ReachBends, v) >= 0: bend = v; break;
                case "bend": throw new ArgumentException($"{call}'s bend is {string.Join(", ", ReachBends.Select(b => $"'{b}'"))}: the side the joint goes; got '{v}'.");
                default: throw new ArgumentException($"{call} has no option '{k}'. It takes {string.Join(", ", ReachOptions)}.");
            }
        }
        return (to, bend);
    }

    /// <summary>
    /// Two-joint IK for one three-segment limb: root, middle joint, end, tip. <paramref name="share"/> is how far
    /// along the tip segment the reaching point is, 0 at the end joint and 1 at the tip.
    /// </summary>
    static (float Upper, float Bend, Point2D Mid, Point2D At, float Miss) ReachLimb(
        IDictionary fig, string limb, string[] joints, object point, float share, string bend, string defaultBend, string call)
    {
        var target = ExtractPoint(point, float.NaN, float.NaN);
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y))
            throw new ArgumentException($"{call}'s point is a point on the page: {{ x, y }} or [x, y].", nameof(point));

        var chain = JsInterop.AsDict(fig[limb]) ?? throw new ArgumentException($"{call}: the figure has no {limb}.");
        Point2D root = ExtractPoint(chain[joints[0]]), mid = ExtractPoint(chain[joints[1]]),
            end = ExtractPoint(chain[joints[2]]), tip = ExtractPoint(chain[joints[3]]);
        float upperLen = SegmentLength(root, mid), lowerLen = SegmentLength(mid, end), tipLen = SegmentLength(end, tip);
        var currentUpper = SegmentAngle(root, mid);
        var currentBend = Wrap180(SegmentAngle(mid, end) - currentUpper);
        var tipOffset = SegmentAngle(end, tip) - SegmentAngle(mid, end);

        // The middle joint to the reaching point, in the lower segment's own frame: that segment, then the share of the tip.
        var t = tipOffset * MathF.PI / 180f;
        float ex = lowerLen + share * tipLen * MathF.Cos(t), ey = share * tipLen * MathF.Sin(t);
        var reachLen = MathF.Sqrt(ex * ex + ey * ey);
        var delta = MathF.Atan2(ey, ex) * 180f / MathF.PI;

        // The distance a two-joint chain can span, and the root angle off the target line by the law of cosines.
        var wanted = SegmentLength(root, target);
        var span = Math.Clamp(wanted, MathF.Abs(upperLen - reachLen), upperLen + reachLen);
        var baseDeg = wanted < 1e-4f ? currentUpper : SegmentAngle(root, target);
        var cos = span < 1e-4f ? 1f : (upperLen * upperLen + span * span - reachLen * reachLen) / (2f * upperLen * span);
        var alpha = MathF.Acos(Math.Clamp(cos, -1f, 1f)) * 180f / MathF.PI;
        var reachedAt = AlongSegment(root, span, baseDeg);

        (float Upper, float Bend, Point2D Mid) Solve(float sign)
        {
            var upper = baseDeg + sign * alpha;
            var m = AlongSegment(root, upperLen, upper);
            var lower = SegmentAngle(m, reachedAt) - delta;
            return (Wrap180(upper), Wrap180(lower - upper), m);
        }

        // Distance from the body's centre line, pelvis to neck, for `out` and `in`.
        var pelvis = ExtractPoint(JsInterop.AsDict(fig["pelvis"])?["center"]);
        var neck = ExtractPoint(fig["neck"]);
        float FromAxis(Point2D p)
        {
            float ax = neck.X - pelvis.X, ay = neck.Y - pelvis.Y, l = MathF.Max(1e-4f, MathF.Sqrt(ax * ax + ay * ay));
            return MathF.Abs(ax * (p.Y - pelvis.Y) - ay * (p.X - pelvis.X)) / l;
        }

        var a = Solve(1f);
        var b = Solve(-1f);
        (float Upper, float Bend, Point2D Mid) By(string side) => side switch
        {
            "down" => a.Mid.Y >= b.Mid.Y ? a : b,
            "up" => a.Mid.Y <= b.Mid.Y ? a : b,
            "left" => a.Mid.X <= b.Mid.X ? a : b,
            "right" => a.Mid.X >= b.Mid.X ? a : b,
            "out" => FromAxis(a.Mid) >= FromAxis(b.Mid) ? a : b,
            _ => FromAxis(a.Mid) <= FromAxis(b.Mid) ? a : b,
        };
        // The standing limbs bend up to about 19 degrees, which is the canon and not a choice.
        var pick = bend != "" ? By(bend)
            : MathF.Abs(currentBend) > 30f ? (MathF.Sign(a.Bend) == MathF.Sign(currentBend) ? a : b)
            : By(defaultBend);
        return (pick.Upper, pick.Bend, pick.Mid, reachedAt, SegmentLength(reachedAt, target));
    }

    static Dictionary<string, object?> ReachResult((float Upper, float Bend, Point2D Mid, Point2D At, float Miss) r,
        string rootKey, string bendKey, string midName, string to) => new()
    {
        ["pose"] = new Dictionary<string, object?> { [rootKey] = r.Upper, [bendKey] = r.Bend },
        [rootKey] = r.Upper,
        [bendKey] = r.Bend,
        ["reached"] = r.Miss <= 0.5f,
        ["miss"] = r.Miss,
        ["at"] = ToDict(r.At),
        [midName] = ToDict(r.Mid),
        ["to"] = to
    };

    static float Wrap180(float degrees)
    {
        var d = degrees % 360f;
        return d > 180f ? d - 360f : d <= -180f ? d + 360f : d;
    }
    #endregion

    #region Figure Geometry
    /// <summary>
    /// Every tapering mass in the figure, named, with the radii <see cref="DrawMannequinSolid"/>
    /// already paints. One table so bounds and geometry can never disagree about the same figure.
    /// </summary>
    static List<(string Name, string Group, Point2D A, Point2D B, float RA, float RB)> FigureBones(IDictionary fig, float H)
    {
        var lArm = JsInterop.AsDict(fig["leftArm"]);
        var rArm = JsInterop.AsDict(fig["rightArm"]);
        var lLeg = JsInterop.AsDict(fig["leftLeg"]);
        var rLeg = JsInterop.AsDict(fig["rightLeg"]);
        var clav = JsInterop.AsDict(fig["clavicles"]);
        var pelvis = JsInterop.AsDict(fig["pelvis"]);

        var neck = ExtractPoint(fig["neck"]);
        var sternum = ExtractPoint(fig["sternum"]);
        var pelCenter = ExtractPoint(pelvis?["center"]);

        // A figure carries its own widths (a child's limbs are thinner for its head than an adult's);
        // one built before builds existed has none, and its head unit is its width.
        var build = JsInterop.AsDict(fig["build"]);
        var W = Num(build, "widthUnit", H);
        var waist = Num(build, "waistUnit", W);
        var arm = Num(build, "armUnit", W);
        var leg = Num(build, "legUnit", W);

        var bones = new List<(string, string, Point2D, Point2D, float, float)>
        {
            ("neck", "torso", neck, sternum, W * 0.18f, W * 0.34f),
            ("shoulders", "torso", ExtractPoint(clav?["left"]), ExtractPoint(clav?["right"]), W * 0.29f, W * 0.29f)
        };

        // A waist bent by a line of action is two pieces meeting at the navel, or the capsule would cut
        // straight across the inside of the curve. Only then: an unbent figure keeps its one capsule,
        // so every figure drawn before lineOfAction existed keeps its silhouette to the pixel.
        if (Num(JsInterop.AsDict(fig["lineOfAction"]), "waistDeg", 0f) != 0f)
        {
            var navel = ExtractPoint(fig["navel"]);
            bones.Insert(1, ("spine", "torso", sternum, navel, waist * 0.55f, waist * 0.575f));
            bones.Insert(2, ("spine", "torso", navel, pelCenter, waist * 0.575f, waist * 0.58f));
        }
        else bones.Insert(1, ("spine", "torso", sternum, pelCenter, waist * 0.55f, waist * 0.58f));

        void Limb(IDictionary? d, string side, string group, string j0, string j1, string j2, string j3,
                  string n0, string n1, string n2, float r0, float r1, float r2, float r3)
        {
            if (d == null) return;
            Point2D p0 = ExtractPoint(d[j0]), p1 = ExtractPoint(d[j1]), p2 = ExtractPoint(d[j2]), p3 = ExtractPoint(d[j3]);
            bones.Add((side + n0, group, p0, p1, r0, r1));
            bones.Add((side + n1, group, p1, p2, r1, r2));
            bones.Add((side + n2, group, p2, p3, r2, r3));
        }

        Limb(lArm, "left", "leftArm", "shoulder", "elbow", "wrist", "hand",
            "UpperArm", "Forearm", "Hand", arm * 0.22f, arm * 0.16f, arm * 0.12f, arm * 0.10f);
        Limb(rArm, "right", "rightArm", "shoulder", "elbow", "wrist", "hand",
            "UpperArm", "Forearm", "Hand", arm * 0.22f, arm * 0.16f, arm * 0.12f, arm * 0.10f);
        Limb(lLeg, "left", "leftLeg", "hip", "knee", "ankle", "foot",
            "Thigh", "Shin", "Foot", leg * 0.28f, leg * 0.20f, leg * 0.14f, leg * 0.10f);
        Limb(rLeg, "right", "rightLeg", "hip", "knee", "ankle", "foot",
            "Thigh", "Shin", "Foot", leg * 0.28f, leg * 0.20f, leg * 0.14f, leg * 0.10f);

        return bones;
    }

    /// <summary>The three elliptical masses, each with the orientation the pose gave it.</summary>
    static List<(string Name, string Group, Point2D C, float Rx, float Ry, float Deg)> FigureMasses(IDictionary fig, float H)
    {
        var head = JsInterop.AsDict(fig["head"]);
        var rib = JsInterop.AsDict(fig["ribcage"]);
        var pel = JsInterop.AsDict(fig["pelvis"]);

        return
        [
            ("head", "head", ExtractPoint(head?["center"]), Num(head, "rx", H * 0.36f), Num(head, "ry", H * 0.50f), Num(head, "angleDeg", 0f)),
            ("ribcage", "torso", ExtractPoint(rib?["center"]), Num(rib, "rx", H * 0.85f), Num(rib, "ry", H * 0.70f), Num(rib, "tiltDeg", 0f)),
            ("pelvis", "torso", ExtractPoint(pel?["center"]), Num(pel, "rx", H * 0.70f), Num(pel, "ry", H * 0.45f), Num(pel, "tiltDeg", 0f))
        ];
    }

    /// <summary>Closed-form extent of every mass, without building a single path.</summary>
    static Dictionary<string, object?> FigureBounds(IDictionary fig, float padding = 0f)
    {
        var H = Num(fig as IDictionary, "headUnit", 70f);
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;

        void Add(float cx, float cy, float hw, float hh)
        {
            x0 = MathF.Min(x0, cx - hw); y0 = MathF.Min(y0, cy - hh);
            x1 = MathF.Max(x1, cx + hw); y1 = MathF.Max(y1, cy + hh);
        }

        foreach (var b in FigureBones(fig, H))
        {
            Add(b.A.X, b.A.Y, b.RA + padding, b.RA + padding);
            Add(b.B.X, b.B.Y, b.RB + padding, b.RB + padding);
        }
        foreach (var m in FigureMasses(fig, H))
        {
            // Exact half-extents of an ellipse rotated by theta.
            var t = m.Deg * MathF.PI / 180f;
            float c = MathF.Cos(t), s = MathF.Sin(t), rx = m.Rx + padding, ry = m.Ry + padding;
            Add(m.C.X, m.C.Y, MathF.Sqrt(rx * rx * c * c + ry * ry * s * s), MathF.Sqrt(rx * rx * s * s + ry * ry * c * c));
        }

        if (x0 > x1) return new Dictionary<string, object?>
        {
            ["x"] = 0f, ["y"] = 0f, ["width"] = 0f, ["height"] = 0f,
            ["x2"] = 0f, ["y2"] = 0f, ["cx"] = 0f, ["cy"] = 0f
        };

        return new Dictionary<string, object?>
        {
            ["x"] = x0, ["y"] = y0, ["width"] = x1 - x0, ["height"] = y1 - y0,
            ["x2"] = x1, ["y2"] = y1, ["cx"] = (x0 + x1) * 0.5f, ["cy"] = (y0 + y1) * 0.5f
        };
    }

    /// <summary>
    /// The figure as geometry rather than as a drawing: one silhouette, a path per mass, the coarse
    /// groups those masses belong to, and the bounds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>drawMannequinSolid</c> builds these same masses and hands nothing back, so every caller
    /// that wanted to clip to a limb, occlude a figure, cut a garment, or fit a figure to a panel
    /// had to rebuild them by hand. A path composes — it fills, clips, strokes, takes boolean
    /// operations, converts through <c>strokeToPath</c>, and survives into <c>outSvg</c>; a draw
    /// call composes with nothing.
    /// </para>
    /// <para>
    /// Separate from <see cref="CreateMannequinFigure"/> because paths are not free: this builds
    /// about twenty native paths and some fifty boolean operations, which a loop measuring poses
    /// should not pay for. <c>figure.bounds</c> lives on the figure itself and costs nothing.
    /// </para>
    /// <para>
    /// <c>padding</c> inflates every mass, which is how a garment is derived: cloth covers the
    /// figure without fitting it, and that gap is where every fold comes from (Studio Manual 22).
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateFigureGeometry(object figureObj, object? options = null)
    {
        if (JsInterop.AsDict(figureObj) is not IDictionary fig)
            throw new ArgumentException("createFigureGeometry needs a figure from Drawing.createMannequinFigure(...).", nameof(figureObj));

        var opt = JsInterop.AsDict(options);
        var padding = Num(opt, "padding", 0f);
        var H = Num(fig, "headUnit", 70f);

        var parts = new Dictionary<string, object?>();
        var groups = new Dictionary<string, CanvasPath>();
        var partGroups = new Dictionary<string, object?>();

        void Record(string name, string group, CanvasPath path)
        {
            partGroups[name] = group;
            // A bent spine arrives as two bones; they are still one part.
            parts[name] = parts.TryGetValue(name, out var had) && had is CanvasPath earlier ? earlier.Union(path) : path;
            groups[group] = groups.TryGetValue(group, out var acc) ? acc.Union(path) : path;
        }

        foreach (var b in FigureBones(fig, H))
            Record(b.Name, b.Group, Capsule(b.A, b.B, b.RA + padding, b.RB + padding));
        foreach (var m in FigureMasses(fig, H))
            Record(m.Name, m.Group, OrientedEllipse(m.C, m.Rx + padding, m.Ry + padding, m.Deg));

        CanvasPath? whole = null;
        var groupDict = new Dictionary<string, object?>();
        foreach (var kv in groups)
        {
            groupDict[kv.Key] = kv.Value;
            whole = whole == null ? kv.Value : whole.Union(kv.Value);
        }

        return new Dictionary<string, object?>
        {
            ["silhouette"] = whole == null ? new CanvasPath() : whole.Simplify(),
            ["parts"] = parts,
            ["groups"] = groupDict,
            ["partGroups"] = partGroups,
            ["bounds"] = FigureBounds(fig, padding),
            ["padding"] = padding,
            // Construction order, NOT depth — the toolkit has no z, so this is the order
            // `drawMannequinSolid` paints in and nothing more. A pose where an arm passes behind the
            // torso still needs the caller to say so.
            ["order"] = new List<object?> { "leftLeg", "rightLeg", "torso", "leftArm", "rightArm", "head" }
        };
    }

    /// <summary>
    /// The coarse group a figure part belongs to, for geometry built before <c>partGroups</c> was returned.
    /// </summary>
    static string PartGroup(string part) => part switch
    {
        "head" => "head",
        "neck" or "spine" or "shoulders" or "ribcage" or "pelvis" => "torso",
        _ when part.StartsWith("left", StringComparison.Ordinal) && (part.EndsWith("Arm", StringComparison.Ordinal) || part.EndsWith("Forearm", StringComparison.Ordinal) || part.EndsWith("Hand", StringComparison.Ordinal)) => "leftArm",
        _ when part.StartsWith("right", StringComparison.Ordinal) && (part.EndsWith("Arm", StringComparison.Ordinal) || part.EndsWith("Forearm", StringComparison.Ordinal) || part.EndsWith("Hand", StringComparison.Ordinal)) => "rightArm",
        _ when part.StartsWith("left", StringComparison.Ordinal) => "leftLeg",
        _ when part.StartsWith("right", StringComparison.Ordinal) => "rightLeg",
        _ => "torso"
    };
    #endregion
}
