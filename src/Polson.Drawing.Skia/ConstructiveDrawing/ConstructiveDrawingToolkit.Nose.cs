namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public partial class ConstructiveDrawingToolkit
{
    #region Nose Solid
    static readonly string[] NoseSolidOptions =
        ["yawDeg", "pitchDeg", "ball", "septum", "septumWidth", "wingSpan", "ballSquare", "wingHeight", "groove", "variation", "build"];

    /// <summary>
    /// The nose's shape, each -1 to +1 and 0 by default. <c>Ball</c>, <c>Septum</c> (and <c>nostrils</c>, which only the
    /// drawer reads) are Hamm's p. 13 catalogue; the rest are his p. 15 variations: <c>SeptumWidth</c> from very narrow
    /// to wide, furrowed and squared onto the lip; <c>WingSpan</c> to wings extra wide apart; <c>BallSquare</c> from a
    /// rounded ball to a squared one with a flattened under surface; <c>WingHeight</c> from low flat wings to high
    /// rounded ones; <c>Groove</c> from a thick undefined ball to the wing groove brought forward and down, tip flattened.
    /// </summary>
    readonly record struct NoseShape(float Ball, float Septum, float SeptumWidth, float WingSpan, float BallSquare, float WingHeight, float Groove);

    /// <summary>
    /// Hamm's six p. 15 variations as settings: the shape each figure shows, and the view for the two that are views.
    /// Any option given beside a variation overrides it. The amounts are the studio's reading of the figures.
    /// </summary>
    static readonly Dictionary<string, Dictionary<string, float>> NoseVariations = new()
    {
        ["wideSeptum"] = new() { ["septumWidth"] = 1f, ["septum"] = 0.3f },                        // wide furrowed septum squared onto the lip
        ["underView"] = new() { ["pitchDeg"] = -35f, ["septumWidth"] = -1f },                      // under view, the wings' attachment, very narrow septum
        ["topView"] = new() { ["pitchDeg"] = 30f, ["wingSpan"] = 1f, ["ballSquare"] = -1f },       // top view, wings extra wide apart, rounded ball
        ["squaredBall"] = new() { ["ballSquare"] = 1f, ["wingHeight"] = -1f },                     // squared ball, flattened under surface, low flat wing
        ["highWings"] = new() { ["wingHeight"] = 1f, ["groove"] = -1f, ["ball"] = 0.5f },          // high rounded wings, thick undefined ball
        ["grooveForward"] = new() { ["groove"] = 1f }                                              // wing groove forward and down, tip flattened
    };

    /// <summary>Reads the shape options, starting from a named <c>variation</c> when one is given, and the pitch.</summary>
    static (NoseShape Shape, float? Pitch, string? Variation) ReadNoseShape(IDictionary? opt, string call)
    {
        var name = opt?["variation"]?.ToString()?.Trim();
        Dictionary<string, float> preset = [];
        if (!string.IsNullOrEmpty(name) && !NoseVariations.TryGetValue(name, out preset!))
            throw new ArgumentException($"{call} variation not recognised: '{name}'. Accepted: {string.Join(", ", NoseVariations.Keys)}.");
        bool Given(string key) => opt != null && opt.Contains(key) && opt[key] is not null;
        float Value(string key) => Math.Clamp(Given(key) ? Num(opt, key, 0f) : preset.GetValueOrDefault(key), -1f, 1f);
        float? pitch = Given("pitchDeg") ? Num(opt, "pitchDeg", 0f) : preset.TryGetValue("pitchDeg", out var p) ? p : null;
        return (new NoseShape(Value("ball"), Value("septum"), Value("septumWidth"), Value("wingSpan"), Value("ballSquare"),
                              Value("wingHeight"), Value("groove")),
                pitch is { } deg ? deg * MathF.PI / 180f : null, string.IsNullOrEmpty(name) ? null : name);
    }

    /// <summary>
    /// The nose as a solid (Hamm, <i>Drawing the Head and Figure</i>, pp. 13–15): its planes as shapes on the page,
    /// each with the direction it faces, so the nose can be lit plane by plane.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built from Hamm's p. 15 cross-sections: narrowest at the top between the eyes, widest at the base where bone
    /// becomes cartilage, the sides nearly perpendicular to the face; the nasal bone runs almost halfway (p. 13), then
    /// the cartilage, the ball, the wings either side, and the plane under the ball back to the septum. Every station
    /// is placed off the <c>noseWedge</c> landmarks, so the solid follows the character parameters, an expression and
    /// the turn. The proportions inside the nose are the studio's reading of the plates.
    /// </para>
    /// <para>
    /// Returns <c>{ planes, yawDeg, ridge, profile, tip }</c>; each plane is
    /// <c>{ name, group, side, path, normal, facing, visible }</c>, the same form as <c>createHeadPlanes</c>, so
    /// <c>drawHeadPlanes</c> lights it. <c>build</c> is <c>'male'</c> (coarser) or <c>'female'</c> (more delicate).
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateNoseSolid(object noseObj, object? options = null)
    {
        if (JsInterop.AsDict(noseObj) is not IDictionary nose)
            throw new ArgumentException("createNoseSolid needs a nose, such as head.noseWedge from Drawing.createLoomisHead(...).",
                nameof(noseObj));
        var opt = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(opt, NoseSolidOptions, "createNoseSolid option");

        float? Angle(string key) => opt != null && opt.Contains(key) && opt[key] is not null ? Num(opt, key, 0f) * MathF.PI / 180f : null;
        var (shape, pitch, _) = ReadNoseShape(opt, "createNoseSolid");
        var solid = BuildNoseSolid(nose, shape, NoseBuild(opt, "createNoseSolid"), Angle("yawDeg"), pitch);

        return new Dictionary<string, object?>
        {
            ["planes"] = solid.Facets.Select(f => (object?)f.ToDict()).ToList(),
            ["yawDeg"] = solid.Yaw * 180f / MathF.PI,
            ["ridge"] = solid.Ridge,
            ["profile"] = solid.Profile,
            ["tip"] = ToDict(solid.Tip)
        };
    }

    /// <summary>One plane of the nose: its outline on the page and the direction it faces, in the head's frame and turned.</summary>
    sealed record NoseFacet(string Name, string Group, string Side, CanvasPath Path, Vec3 Normal, Vec3 Facing)
    {
        public bool Visible => Facing.Z > 0.02f && !Path.Path.IsEmpty;

        public Dictionary<string, object?> ToDict() => new()
        {
            ["name"] = Name,
            ["group"] = Group,
            ["side"] = Side,
            ["path"] = Path,
            ["normal"] = Normal.ToDict(),
            ["facing"] = Facing.ToDict(),
            ["visible"] = Visible
        };
    }

    readonly record struct Vec3(float X, float Y, float Z)
    {
        public Vec3 Unit() { var l = MathF.Sqrt((X * X) + (Y * Y) + (Z * Z)); return l > 0f ? new(X / l, Y / l, Z / l) : this; }
        public float Dot(Vec3 o) => (X * o.X) + (Y * o.Y) + (Z * o.Z);
        public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);
        public Dictionary<string, object?> ToDict() => new() { ["x"] = X, ["y"] = Y, ["z"] = Z };
    }

    /// <summary>The solid, its turn, and the lines that come off it.</summary>
    /// <param name="ProfileSide">Which way on the page the tip swings, -1, 0 or 1: the side whose edge is the nose's profile.</param>
    /// <param name="Turn">How far turned, 0 frontally to 1 by about 30 degrees, for the weight of the profile line.</param>
    /// <param name="ProfileCurl">Where the profile turns round under the tip toward the septum, when the nose is turned.</param>
    sealed record NoseSolidModel(List<NoseFacet> Facets, float Yaw, float Pitch, Point2D Tip, CanvasPath Ridge, CanvasPath Profile,
                                 Point2D[] ProfileCurl, float ProfileSide, float Turn);

    /// <summary>
    /// A normal in the head's frame (x toward the near side, y down, z out of the face) turned with the head: pitch
    /// about x, then yaw about y, positive yaw turning the face toward -x, as <c>createHeadPlanes</c> turns its planes.
    /// </summary>
    static Vec3 TurnNormal(Vec3 n, float yaw, float pitch)
    {
        n = n.Unit();
        float sy = MathF.Sin(yaw), cy = MathF.Cos(yaw), sp = MathF.Sin(pitch), cp = MathF.Cos(pitch);
        float y1 = (cp * n.Y) + (sp * n.Z), z1 = (-sp * n.Y) + (cp * n.Z);
        return new((cy * n.X) - (sy * z1), y1, (sy * n.X) + (cy * z1));
    }

    /// <summary><c>build</c>: 'male' is coarser (+1), 'female' more delicate (-1), left out neither.</summary>
    static float NoseBuild(IDictionary? opt, string call)
    {
        var raw = opt?["build"]?.ToString()?.Trim().ToLowerInvariant();
        return raw switch
        {
            null or "" => 0f,
            "male" => 1f,
            "female" => -1f,
            _ => throw new ArgumentException($"{call} build must be 'male' or 'female', not '{raw}'.")
        };
    }

    /// <summary>
    /// Lofts the nose through five cross-sections and closes it underneath.
    /// </summary>
    /// <remarks>
    /// Coordinates inside the nose: <c>x</c> across in base widths (one eye; the wings' outer edges at ±0.5),
    /// <c>v</c> down from <c>bridgeTop</c> in nose lengths, <c>z</c> out of the face in nose lengths. They land on
    /// the page the way the construction places the face: the near half at full width, the far half foreshortened by
    /// the far wing's own ratio, and depth swung sideways by exactly the amount that puts the tip on <c>apex</c>. So the
    /// solid agrees with the landmarks it came from, whatever moved them. The normals are turned by the yaw that ratio
    /// implies; pitch is not recoverable from the nose alone and is 0 unless given.
    /// </remarks>
    static NoseSolidModel BuildNoseSolid(IDictionary nose, NoseShape shape, float coarse, float? yawOverride, float? pitchOverride)
    {
        var (ball, septumShape, septumWidth, wingSpan, ballSquare, wingHeight, groove) = shape;
        var bridgeTop = ExtractPoint(nose["bridgeTop"]);
        var apex = ExtractPoint(nose["apex"]);
        var underNose = ExtractPoint(nose["underNose"]);
        var nearNostril = ExtractPoint(nose["nearNostril"]);
        var hasFar = nose.Contains("farNostril") && nose["farNostril"] is not null;

        var len = MathF.Abs(underNose.Y - bridgeTop.Y);
        if (len <= 0.1f) len = NoseLengthAt240;
        var nearHalf = MathF.Abs(nearNostril.X - underNose.X);
        if (nearHalf < 0.1f) nearHalf = len * 0.25f;
        var farHalf = hasFar ? MathF.Abs(ExtractPoint(nose["farNostril"]).X - underNose.X) : nearHalf;
        var width = nearHalf * 2f;
        var farRatio = Math.Clamp(farHalf / nearHalf, 0f, 1f);
        var nearSign = nearNostril.X >= underNose.X ? 1f : -1f;

        float AxisX(float v) => bridgeTop.X + ((underNose.X - bridgeTop.X) * v);
        var vTip = Math.Clamp((apex.Y - bridgeTop.Y) / len, 0.6f, 0.95f);
        var tipOff = apex.X - AxisX(vTip);
        var profileSide = MathF.Abs(tipOff) > len * 0.002f ? MathF.Sign(tipOff) : 0f;

        var yaw = yawOverride ?? (-profileSide * MathF.Acos(farRatio));
        var pitch = pitchOverride ?? 0f;

        // **The shape.** A larger ball is wider at the front, stands further out and starts its underside nearer the
        // tip, so the plane beneath it is taller; a coarser nose is a little broader and bolder in the ball.
        // From p. 15: a squared ball is broader at the front, and a thick undefined ball broader still; the wing groove
        // brought forward flattens the tip; wings set wide apart stand out past the base's own width.
        var front = 1f + (0.35f * ball) + (0.12f * coarse) + (0.12f * ballSquare) + (0.2f * MathF.Max(0f, -groove));
        var depth = 1f + (0.12f * ball) + (0.08f * coarse);
        var tipZ = 0.44f * depth * (1f - (0.2f * MathF.Max(0f, groove)));
        var span = 1f + (0.25f * wingSpan);
        var depthToX = tipOff / (tipZ * len);
        var septumDrop = septumShape >= 0f ? 0.045f * septumShape : 0.012f * septumShape;

        Point2D Page(Vec3 p)
        {
            var across = p.X >= 0f ? p.X : p.X * farRatio;
            // Pitch swings depth up or down the page: nodding down, what stands out drops.
            return new Point2D(AxisX(p.Y) + (nearSign * across * width) + (depthToX * p.Z * len), bridgeTop.Y + (p.Y * len) + (MathF.Sin(pitch) * p.Z * len));
        }

        // The stations in the nose's own frame, then measured in pixels for the normals.
        Vec3 Metric(Vec3 p) => new(p.X * width, p.Y * len, p.Z * len);

        // Five cross-sections: base corner (on the face) and front corner (the edge of the bridge or ball), per side.
        // Narrowest between the eyes; the nasal bone ends near halfway; the base widens where it becomes cartilage;
        // the ball widest at the tip, with the wings out at the nose's full width on the face.
        float vBone = 0.45f, vCartilage = vTip - 0.20f, vBallTop = vTip - 0.08f, vUnder = vTip + 0.04f - (0.05f * ball);
        (float V, float BaseX, float BaseV, float BaseZ, float FrontX, float FrontZ)[] rows =
        [
            (0f,         0.13f, 0f,                  0f,    0.045f,          0.08f),
            (vBone,      0.22f, vBone,               0f,    0.07f,           0.22f),
            (vCartilage, 0.28f, vCartilage,          0f,    0.085f * front,  0.32f * depth),
            (vBallTop,   0.46f * span, vBallTop + 0.03f - (wingHeight >= 0f ? 0.06f * wingHeight : 0.04f * wingHeight), 0f,
                         (ballSquare >= 0f ? 0.14f + (0.02f * ballSquare) : 0.14f * (1f + (0.25f * ballSquare))) * front,
                         // The tip flattened by the groove brought forward: the front of the ball stands as far out as the tip.
                         0.40f * depth + ((tipZ - (0.40f * depth)) * MathF.Max(0f, groove))),
            (vUnder,     0.50f * span, vUnder + 0.02f, wingHeight >= 0f ? 0.05f + (0.06f * wingHeight) : 0.05f * (1f + wingHeight),
                         0.16f * front,   tipZ)
        ];
        Vec3 Base(int r, float s) => new(s * rows[r].BaseX, rows[r].BaseV, rows[r].BaseZ);
        Vec3 Front(int r, float s) => new(s * rows[r].FrontX, rows[r].V, rows[r].FrontZ);

        // Under the ball: the wing's lower edge, the ball's underside each side, and the septum where it meets the lip.
        // The groove brought forward and down carries the wing's foot with it; a squared ball's under surface is flatter,
        // its foot further out; a wide septum widens the ball's underside and squares onto the lip in two corners.
        var grooveOn = MathF.Max(0f, groove);
        Vec3 WingFoot(float s) => new(s * 0.38f * span, 0.985f + (0.01f * grooveOn), 0.10f + (0.06f * grooveOn));
        Vec3 BallFoot(float s) => new(s * 0.10f * front * (1f + (0.6f * septumWidth)),
            0.975f - (0.04f * MathF.Max(0f, ballSquare)) + (0.015f * MathF.Max(0f, -ballSquare)), (0.30f + (0.08f * ballSquare)) * depth);
        var septumHalf = 0.06f * MathF.Max(0f, septumWidth);
        Vec3 Septum(float s) => new(s * septumHalf, 1f + septumDrop, 0.02f);
        var septum = Septum(0f);

        var facets = new List<NoseFacet>();
        void Add(string name, string group, string side, params Vec3[] pts)
        {
            var path = new CanvasPath();
            var first = Page(pts[0]);
            path.MoveTo(first.X, first.Y);
            foreach (var p in pts.Skip(1)) { var q = Page(p); path.LineTo(q.X, q.Y); }
            path.ClosePath();

            // Newell's normal from the stations themselves, turned outward: away from a point inside, above and behind.
            var m = pts.Select(Metric).ToArray();
            var n = new Vec3(0f, 0f, 0f);
            for (var i = 0; i < m.Length; i++)
            {
                Vec3 a = m[i], b = m[(i + 1) % m.Length];
                n += new Vec3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
            }
            var centroid = m.Aggregate(new Vec3(0f, 0f, 0f), (s, p) => s + p) * (1f / m.Length);
            var inside = new Vec3(0f, centroid.Y - (0.5f * len), -0.5f * len);
            if (n.Dot(centroid - inside) < 0f) n *= -1f;
            n = n.Unit();
            facets.Add(new NoseFacet(name, group, side, path, n, TurnNormal(n, yaw, pitch)));
        }

        string[] groups = ["bridge", "bridge", "side", "wing"];
        string[] names = ["Bridge", "Cartilage", "BallSide", "Wing"];
        for (var r = 0; r < 4; r++)
        {
            Add(r < 2 ? (r == 0 ? "bridge" : "cartilage") : (r == 2 ? "ballTop" : "ball"), r < 2 ? "bridge" : "ball", "center",
                Front(r, -1f), Front(r, 1f), Front(r + 1, 1f), Front(r + 1, -1f));
            foreach (var (side, s) in new[] { ("far", -1f), ("near", 1f) })
                Add(side + names[r], r < 2 ? "side" : groups[r], side, Base(r, s), Front(r, s), Front(r + 1, s), Base(r + 1, s));
        }
        if (septumHalf > 0f) Add("under", "under", "center", Front(4, -1f), Front(4, 1f), BallFoot(1f), Septum(1f), Septum(-1f), BallFoot(-1f));
        else Add("under", "under", "center", Front(4, -1f), Front(4, 1f), BallFoot(1f), septum, BallFoot(-1f));
        foreach (var (side, s) in new[] { ("far", -1f), ("near", 1f) })
            Add(side + "WingBottom", "under", side, Base(4, s), Front(4, s), BallFoot(s), WingFoot(s));

        // The centre of the bridge and ball, and the edge of the front plane on the side the tip swings toward,
        // which is the nose's profile once the head turns (Hamm, p. 15, "actual profile").
        var ridge = new CanvasPath();
        var ridgeStart = Page(new Vec3(0f, 0f, rows[0].FrontZ));
        ridge.MoveTo(ridgeStart.X, ridgeStart.Y);
        for (var r = 1; r < rows.Length; r++) { var p = Page(new Vec3(0f, rows[r].V, rows[r].FrontZ)); ridge.LineTo(p.X, p.Y); }

        var profile = new CanvasPath();
        Point2D[] curl = [];
        if (profileSide != 0f)
        {
            var frameSide = profileSide * nearSign;
            var start = Page(Front(0, frameSide));
            profile.MoveTo(start.X, start.Y);
            for (var r = 1; r < rows.Length; r++) { var p = Page(Front(r, frameSide)); profile.LineTo(p.X, p.Y); }
            // A squared ball turns a corner under the tip rather than rolling round it.
            var corner = MathF.Max(0f, ballSquare);
            curl = corner > 0f
                ? [Page(new Vec3(frameSide * rows[4].FrontX, vUnder + (0.09f * corner), tipZ * (1f - (0.03f * corner)))),
                   Page(new Vec3(frameSide * rows[4].FrontX * 0.8f, vUnder + (0.11f * corner), tipZ * (1f - (0.15f * corner)))), Page(BallFoot(frameSide)),
                   Page(new Vec3(-frameSide * 0.06f, 0.995f + septumDrop, 0.12f))]
                : [Page(BallFoot(frameSide)), Page(new Vec3(-frameSide * 0.06f, 0.995f + septumDrop, 0.12f))];
        }

        var turn = SmoothStep(0.05f, 0.5f, MathF.Abs(MathF.Sin(yaw)));
        return new NoseSolidModel(facets, yaw, pitch, Page(new Vec3(0f, vUnder, tipZ)), ridge, profile, curl, profileSide, turn);
    }

    /// <summary>The union of some facets, and the facing of the whole, averaged by area.</summary>
    static (CanvasPath Path, Vec3 Normal) MergeNoseFacets(IEnumerable<NoseFacet> facets)
    {
        CanvasPath? path = null;
        var n = new Vec3(0f, 0f, 0f);
        foreach (var f in facets)
        {
            path = path is null ? new CanvasPath(f.Path) : path.Union(f.Path);
            n += f.Normal * MathF.Max(1e-3f, f.Path.Area);
        }
        return (path ?? new CanvasPath(), n.Unit());
    }
    #endregion
}
