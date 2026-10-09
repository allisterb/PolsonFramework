namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public partial class ConstructiveDrawingToolkit
{
    #region Comic Nose
    /// <summary>One of Hamm's marks, on one side of the nose, at a weight relative to the nose's tier.</summary>
    /// <param name="Kind">What the mark is; see <see cref="NoseMarkKinds"/>.</param>
    /// <param name="Side">Which side it goes on: both, shadow, lit, near, far or profile.</param>
    readonly record struct NoseMark(string Kind, string Side, float Weight = 1f)
    {
        public override string ToString() => Kind is "base" or "longBase" or "waveBase" or "baseBar" ? Kind : $"{Kind}:{Side}";
    }

    /// <summary>The marks Hamm's front-view noses are made of (<i>Drawing the Head and Figure</i>, p. 14).</summary>
    static readonly string[] NoseMarkKinds =
        ["slashes", "wings", "doubleWings", "nostrils", "nostrilArcs", "base", "longBase", "waveBase", "baseBar",
         "noseLine", "profile", "shadow", "underShadow", "tipShadow", "hatch"];

    static readonly string[] NoseMarkSides = ["both", "shadow", "lit", "near", "far", "profile"];

    static string DefaultNoseSide(string kind) => kind switch
    {
        "doubleWings" => "near",
        "noseLine" or "shadow" or "underShadow" or "hatch" => "shadow",
        "profile" or "tipShadow" => "profile",
        _ => "both"
    };

    static NoseMark M(string kind, string? side = null, float weight = 1f) => new(kind, side ?? DefaultNoseSide(kind), weight);

    /// <summary>
    /// Hamm's sixteen treatments (p. 14), each the marks of one of his figures: eight front-view female noses and eight
    /// semi-front male ones. He says the patterns may be interchanged, so any of them suits any face. Sides follow his
    /// light from the top right when none is given; "near" is the full wing a turned nose shows, "profile" the side the
    /// tip swings to. The weights are the studio's reading of the figures.
    /// </summary>
    static readonly Dictionary<string, NoseMark[]> NoseTreatments = new()
    {
        ["female1"] = [M("slashes"), M("wings"), M("nostrils")],                                       // ) (  ( ~ - )
        ["female2"] = [M("slashes"), M("longBase", weight: 1.3f)],                                      // the cavities lost in a thin shadow
        ["female3"] = [M("noseLine"), M("wings", "lit"), M("nostrils", "lit")],                         // one line down the side into the wing
        ["female4"] = [M("slashes", "lit", 2.4f), M("shadow")],                                         // a bold crescent; the wing and under-tip in black
        ["female5"] = [M("slashes"), M("wings"), M("base", weight: 1.6f)],                              // ( ‿ )
        ["female6"] = [M("slashes"), M("waveBase", weight: 1.2f)],                                      // a short wavy base alone
        ["female7"] = [M("slashes", "lit"), M("noseLine"), M("hatch"), M("wings", "lit"), M("nostrils", "lit")],
        ["female8"] = [M("slashes"), M("wings", "lit"), M("underShadow")],                              // a black wedge under the shadow wing
        ["male1"] = [M("profile"), M("wings", "near"), M("base", weight: 1.5f)],
        ["male2"] = [M("slashes", "near"), M("profile"), M("doubleWings", "near"), M("base", weight: 1.6f)],
        ["male3"] = [M("slashes", "near"), M("baseBar")],                                               // one slash and a heavy bar
        ["male4"] = [M("noseLine", "near"), M("profile"), M("wings", "near"), M("nostrilArcs", "near"), M("base")],
        ["male5"] = [M("profile"), M("shadow", "near"), M("base", weight: 1.6f)],
        ["male6"] = [M("profile", weight: 0.7f), M("doubleWings", "near"), M("wings", "far"), M("base", weight: 0.8f)],
        ["male7"] = [M("slashes", "near"), M("profile"), M("doubleWings", "near"), M("hatch", "profile")],
        ["male8"] = [M("slashes", "near"), M("profile"), M("tipShadow"), M("wings", "near"), M("nostrilArcs", "near")]
    };

    /// <summary>The treatment, or the marks given instead of one.</summary>
    static (string Name, NoseMark[] Marks) ReadNoseTreatment(IDictionary? opt)
    {
        if (opt?["marks"] is { } given)
        {
            if (opt["treatment"] is not null)
                throw new ArgumentException("drawComicNose takes a treatment or marks, not both: marks replace the treatment's own.");
            IEnumerable items = given is string one ? new[] { one } : given as IEnumerable
                ?? throw new ArgumentException("drawComicNose marks must be a list, such as ['slashes', 'noseLine'].");
            var marks = new List<NoseMark>();
            foreach (var item in items)
            {
                var text = item?.ToString()?.Trim() ?? "";
                var parts = text.Split(':', 2, StringSplitOptions.TrimEntries);
                if (!NoseMarkKinds.Contains(parts[0]))
                    throw new ArgumentException($"drawComicNose mark not recognised: '{parts[0]}'. Accepted: {string.Join(", ", NoseMarkKinds)}.");
                var side = parts.Length > 1 ? parts[1] : DefaultNoseSide(parts[0]);
                if (!NoseMarkSides.Contains(side))
                    throw new ArgumentException($"drawComicNose mark side not recognised: '{side}' in '{text}'. Accepted: {string.Join(", ", NoseMarkSides)}.");
                marks.Add(new NoseMark(parts[0], side));
            }
            return ("custom", [.. marks]);
        }

        var name = opt?["treatment"]?.ToString()?.Trim() ?? "female1";
        return NoseTreatments.TryGetValue(name, out var recipe)
            ? (name, recipe)
            : throw new ArgumentException($"drawComicNose treatment not recognised: '{name}'. Accepted: {string.Join(", ", NoseTreatments.Keys)}.");
    }

    /// <summary>A stroke whose width varies along it: <paramref name="width"/> of the fraction of its length.</summary>
    static CanvasPath VariableStroke(IReadOnlyList<Point2D> pts, Func<float, float> width)
    {
        var path = new CanvasPath();
        if (pts.Count < 2) return path;
        var cum = new float[pts.Count];
        for (var i = 1; i < pts.Count; i++)
            cum[i] = cum[i - 1] + MathF.Sqrt(((pts[i].X - pts[i - 1].X) * (pts[i].X - pts[i - 1].X)) + ((pts[i].Y - pts[i - 1].Y) * (pts[i].Y - pts[i - 1].Y)));
        var total = MathF.Max(cum[^1], 1e-3f);
        var left = new Point2D[pts.Count];
        var right = new Point2D[pts.Count];
        for (var i = 0; i < pts.Count; i++)
        {
            Point2D a = pts[Math.Max(0, i - 1)], b = pts[Math.Min(pts.Count - 1, i + 1)];
            float tx = b.X - a.X, ty = b.Y - a.Y, l = MathF.Max(1e-4f, MathF.Sqrt((tx * tx) + (ty * ty)));
            var wv = width(cum[i] / total);
            float nx = -ty / l, ny = tx / l, w = float.IsFinite(wv) ? MathF.Max(0f, wv) * 0.5f : 0f;
            left[i] = new Point2D(pts[i].X + (nx * w), pts[i].Y + (ny * w));
            right[i] = new Point2D(pts[i].X - (nx * w), pts[i].Y - (ny * w));
        }
        path.MoveTo(left[0].X, left[0].Y);
        for (var i = 1; i < left.Length; i++) path.LineTo(left[i].X, left[i].Y);
        for (var i = right.Length - 1; i >= 0; i--) path.LineTo(right[i].X, right[i].Y);
        path.ClosePath();
        return path;
    }

    /// <summary>A smooth curve through points (uniform Catmull-Rom), sampled.</summary>
    static List<Point2D> SplineThrough(IReadOnlyList<Point2D> pts, int perSegment = 12)
    {
        var result = new List<Point2D>();
        for (var i = 0; i < pts.Count - 1; i++)
        {
            Point2D p0 = pts[Math.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.Min(pts.Count - 1, i + 2)];
            for (var k = 0; k < perSegment; k++)
            {
                float t = (float)k / perSegment, t2 = t * t, t3 = t2 * t;
                float F(float a, float b, float c, float d) => 0.5f * ((2f * b) + ((-a + c) * t) + (((2f * a) - (5f * b) + (4f * c) - d) * t2) + ((-a + (3f * b) - (3f * c) + d) * t3));
                result.Add(new Point2D(F(p0.X, p1.X, p2.X, p3.X), F(p0.Y, p1.Y, p2.Y, p3.Y)));
            }
        }
        result.Add(pts[^1]);
        return result;
    }

    static CanvasPath Polygon(IEnumerable<Point2D> pts)
    {
        var path = new CanvasPath();
        var first = true;
        foreach (var p in pts)
        {
            if (first) { path.MoveTo(p.X, p.Y); first = false; }
            else path.LineTo(p.X, p.Y);
        }
        path.ClosePath();
        return path;
    }

    /// <summary>One wing's geometry: its arc, its centre and radius, and where its opening sits.</summary>
    readonly record struct NoseWing(CanvasPath Arc, Point2D Centre, float R, float ROpen, Point2D Opening, float OpenX, float OpenY,
                                    float Inward, float Rim, float Cavity, Point2D Curl);

    /// <summary>
    /// Draws the nose in one of Hamm's treatments over its solid lit, and <b>returns the parts it built</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hamm draws a nose as a form in light with a few marks over it (<i>Drawing the Head and Figure</i>, pp. 13–15), and
    /// his figures differ in which marks, not only in proportion. <c>treatment</c> names one of his sixteen
    /// (<c>'female1'</c>…<c>'female8'</c>, <c>'male1'</c>…<c>'male8'</c>, default <c>'female1'</c>), and <c>marks</c>
    /// composes your own from the same marks. The tone is <c>createNoseSolid</c>'s planes lit; <c>build</c> sets the
    /// weight, coarse or delicate.
    /// </para>
    /// <para>
    /// The shape, each -1 to +1 and 0 by default, from Hamm's p. 13 catalogue: <c>ball</c> (small to large),
    /// <c>nostrils</c> (hidden to exposed) and <c>septum</c> (tucked into the tip to low-hanging).
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> DrawComicNose(CanvasRenderingContext2D ctx, object noseObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(noseObj) is not IDictionary nose) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var medium = ReadMedium(optDict, "drawComicNose");
        var shadowColor = optDict?["shadowColor"]?.ToString() ?? "#b06f4c";
        var weight = Fraction(optDict, "weight", 1f);
        var depressions = Math.Clamp(Num(optDict, "depressions", 1f), 0f, 1f);
        var hasLight = optDict is not null && optDict.Contains("light") && optDict["light"] is not null;
        var lightDeg = hasLight ? Num(optDict, "light", 0f) : 0f;
        var shade = Math.Clamp(Num(optDict, "shadow", 0.5f), 0f, 1f);
        var hasDetail = optDict is not null && optDict.Contains("detail") && optDict["detail"] is not null;
        var (treatment, recipe) = ReadNoseTreatment(optDict);

        // **Male and female** (Hamm, p. 14): the patterns are interchangeable; the male is usually more coarse, the
        // female more delicate. So `build` sets the weight of whatever treatment is drawn, and firmer or lighter tone;
        // a woman's nose also loses the line along its side, which he calls risky. The amounts are the studio's.
        var coarse = NoseBuild(optDict, "drawComicNose");
        weight *= 1f + (coarse > 0f ? 0.25f : 0.2f) * coarse;
        var toneAmount = Math.Clamp(Num(optDict, "tone", 1f), 0f, 1f) * (1f + (0.25f * coarse));

        // **The nose's shape**, each -1 to +1 and 0 by default, the ways the lower noses in Hamm's p. 13 catalogue
        // differ: `ball` from a small ball to a large one that crowds the wings; `nostrils` from openings barely
        // discernible to large ones running straight across; `septum` from tapered up into the tip to hanging low.
        // And the p. 15 variations (`septumWidth`, `wingSpan`, `ballSquare`, `wingHeight`, `groove`, and `pitchDeg` for his
        // under and top views), or one of his six figures by name as `variation`.
        var (shape, pitch, variation) = ReadNoseShape(optDict, "drawComicNose");
        float ballShape = shape.Ball, nostrilShape = Math.Clamp(Num(optDict, "nostrils", 0f), -1f, 1f), septumShape = shape.Septum;
        float septumWidth = shape.SeptumWidth, ballSquare = shape.BallSquare, wingHeight = shape.WingHeight, groove = shape.Groove;
        var span = 1f + (0.25f * shape.WingSpan);
        // Looking up at the nose (his under view) opens the nostrils and shows the wings' attachment; looking down on it
        // (his top view) hides them behind the ball.
        var pitchSin = MathF.Sin(pitch ?? 0f);
        float under = Math.Clamp(-pitchSin / MathF.Sin(35f * MathF.PI / 180f), 0f, 1f), over = Math.Clamp(pitchSin / MathF.Sin(35f * MathF.PI / 180f), 0f, 1f);
        var septumDrop = septumShape >= 0f ? 0.045f * septumShape : 0.012f * septumShape;

        var bridgeTop = ExtractPoint(nose["bridgeTop"]);
        var apex = ExtractPoint(nose["apex"]);
        var underNose = ExtractPoint(nose["underNose"]);
        var nearNostril = ExtractPoint(nose["nearNostril"]);
        var hasFar = nose.Contains("farNostril") && nose["farNostril"] is not null;
        var farNostril = hasFar ? ExtractPoint(nose["farNostril"]) : underNose;

        var len = MathF.Abs(underNose.Y - bridgeTop.Y);
        if (len <= 0.1f) len = NoseLengthAt240;

        // **The nose is a solid first** (createNoseSolid, Hamm p. 15): its planes take the tone, and its edges and
        // stations are where the marks go. Built from these landmarks, so it follows whatever moved them.
        var solid = BuildNoseSolid(nose, shape, coarse, null, pitch);
        var bridge = solid.Ridge;

        var tierR = len * (NostrilRadiusTier / NoseLengthAt240);
        var nearHalf = MathF.Abs(underNose.X - nearNostril.X);
        var farHalf = hasFar ? MathF.Abs(underNose.X - farNostril.X) : nearHalf;
        var nearSide = nearNostril.X >= underNose.X ? 1f : -1f;

        // **The far wing goes round the ball as the head turns** (Hamm, p. 15): first the opening slips out of sight,
        // then only the wing's rim shows, and the opening is a trace hugging the septum; then the far wing is gone.
        var farRatio = nearHalf > 0.1f ? Math.Clamp(farHalf / nearHalf, 0f, 1f) : 1f;
        var farRim = SmoothStep(0.45f, 0.70f, farRatio);
        var farCavity = SmoothStep(0.62f, 0.92f, farRatio);

        // **Each wing lies inside the nose**: a nostril landmark is the base's outer edge, so the wing's outermost point
        // sits on it and its centre lies inward. Its convex outer arc is Hamm's "convex outer nostril" line (p. 14).
        NoseWing Wing(float side)
        {
            var near = side == nearSide;
            // Wings set extra wide apart stand out past the base's own width (p. 15, top view).
            var edge = Lerp(underNose, near ? nearNostril : farNostril, span);
            edge = new Point2D(edge.X, (near ? nearNostril : farNostril).Y);
            float rim = near ? 1f : hasFar ? farRim : 0f, cavity = near ? 1f : hasFar ? farCavity : 0f;
            // Seen from above, the openings go behind the ball.
            cavity *= 1f - over;
            var inward = -side;
            var halfSpan = MathF.Abs(underNose.X - edge.X);
            // A large ball crowds the wings, so they shrink as it grows; the openings keep their own size. High wings are
            // rounder and sit higher, low ones flatter and lower (p. 15).
            var rOpen = halfSpan > tierR ? halfSpan * 0.26f : MathF.Max(0.5f, halfSpan * 0.4f);
            var r = rOpen * (1f - (0.25f * ballShape)) * (1f + (0.35f * MathF.Max(0f, wingHeight)));
            var ry = r * (1f + (0.45f * wingHeight));
            var c = new Point2D(edge.X + (inward * r), edge.Y - (r * (wingHeight >= 0f ? 0.6f : 0.25f) * wingHeight));
            var outward = inward > 0f ? MathF.PI : 0f;
            // The wing groove brought forward carries the curl further round under; an undefined ball leaves only the rim.
            float up = 75f * MathF.PI / 180f * (0.4f + (0.6f * rim)) * (1f + (0.35f * MathF.Min(0f, groove))),
                  curl = 85f * MathF.PI / 180f * (0.2f + (0.8f * cavity)) * (groove >= 0f ? 1f + (0.7f * groove) : 1f + (0.7f * groove))
                         + (30f * MathF.PI / 180f * under);
            var arc = new CanvasPath();
            if (rim > 0.02f)
            {
                if (inward < 0f) arc.Ellipse(c.X, c.Y, r, ry, 0f, outward - up, outward + curl);
                else arc.Ellipse(c.X, c.Y, r, ry, 0f, outward + up, outward - curl, true);
            }
            // The opening shrinks to a trace and slides in against the septum, just on its own side; a narrow septum
            // brings the two together, a wide one parts them; the groove brought forward and down carries it with it.
            var open = new Point2D(c.X + (inward * r * (0.7f + (0.2f * MathF.Max(0f, groove)))), c.Y + (ry * 0.55f) + (r * 0.25f * MathF.Max(0f, groove)));
            open = new Point2D(open.X + (inward * r * 0.3f * -septumWidth), open.Y - (under * r * 0.5f));
            var trace = new Point2D(underNose.X - (inward * r * 0.35f), underNose.Y - (r * 0.05f));
            var at = Lerp(trace, open, cavity);
            // Exposed openings grow mostly across, as Hamm's "large straight-across nostrils" do; hidden ones to slits.
            // From below they open further, deeper than wide.
            // From above they close up behind the ball.
            float openX = (1f + (0.7f * nostrilShape)) * (1f + (0.3f * under)) * (1f - over),
                  openY = (nostrilShape >= 0f ? 1f + (0.3f * nostrilShape) : 1f + (0.5f * nostrilShape)) * (1f + (1.6f * under)) * (1f - (0.5f * over));
            return new NoseWing(arc, c, r, rOpen, at, openX, openY, inward, rim, cavity, new Point2D(c.X + (inward * r * 1.2f), c.Y + (ry * 0.55f)));
        }

        Point2D Ridge(float y)
        {
            var s = MathF.Abs(apex.Y - bridgeTop.Y) > 0.1f ? Math.Clamp((y - bridgeTop.Y) / (apex.Y - bridgeTop.Y), 0f, 1f) : 0f;
            return new Point2D(bridgeTop.X + ((apex.X - bridgeTop.X) * s), y);
        }
        float Half(float side) => side == nearSide ? nearHalf : farHalf;

        // Which side is in shadow, when a light is given: the side facing away from it. Hamm's figures that imply a light
        // have it from the top right, so with none given the marks that belong to a shadow side go on the left.
        var lightX = hasLight ? MathF.Cos(lightDeg * MathF.PI / 180f) : 0f;
        var shadowSide = MathF.Abs(lightX) > 0.2f ? -MathF.Sign(lightX) : 0f;
        var markShadow = shadowSide != 0f ? shadowSide : -1f;
        var turned = solid.ProfileSide != 0f && solid.Turn > 0.01f;
        var profilePage = turned ? solid.ProfileSide : markShadow;
        float[] Sides(string side) => side switch
        {
            "both" => [nearSide, -nearSide],
            "shadow" => [markShadow],
            "lit" => [-markShadow],
            "near" => [nearSide],
            "far" => [-nearSide],
            _ => [profilePage]
        };

        // **The light on the solid.** Each plane takes the tone its facing earns. With no light given, the light is
        // from above and only the underside darkens: the nose's "little shadow" (p. 14).
        var lightRad = (hasLight ? lightDeg : -90f) * MathF.PI / 180f;
        var lightFront = hasLight ? 0.5f : 0f;
        var lightSide = MathF.Sqrt(1f - (lightFront * lightFront));
        var light = new Vec3(MathF.Cos(lightRad) * lightSide, MathF.Sin(lightRad) * lightSide, lightFront);
        float litAt = hasLight ? 0.3f : 0.05f, darkAt = hasLight ? -0.35f : -0.5f;
        var facetTones = solid.Facets.Where(f => f.Visible)
            .Select(f => (Facet: f, Light: f.Facing.Dot(light),
                          Tone: 0.45f * toneAmount * (hasLight ? 0.6f + (0.4f * shade) : 1f) * Math.Clamp((litAt - f.Facing.Dot(light)) / (litAt - darkAt), 0f, 1f)))
            .ToList();
        var underPlane = MergeNoseFacets(solid.Facets.Where(f => f.Name == "under")).Path;
        var sideShadow = shadowSide == 0f ? new CanvasPath()
            : MergeNoseFacets(facetTones.Where(t => t.Facet.Side == (shadowSide * nearSide > 0f ? "near" : "far") && t.Tone > 0.01f).Select(t => t.Facet)).Path;

        var detail = hasDetail
            ? Math.Clamp((int)MathF.Round(Num(optDict, "detail", 4f)), 1, 4)
            : len >= NoseDetailLength[2] ? 4 : len >= NoseDetailLength[1] ? 3 : len >= NoseDetailLength[0] ? 2 : 1;

        float Ink(float tier, float w = 1f) => Tier(tier, len, NoseLengthAt240, weight * w);
        var septumPt = new Point2D(underNose.X, underNose.Y + (len * (0.01f + septumDrop)));

        // The parts, filled unless named as a stroke.
        var wingArcs = new Dictionary<float, CanvasPath> { [nearSide] = new(), [-nearSide] = new() };
        var openings = new Dictionary<float, CanvasPath> { [nearSide] = new(), [-nearSide] = new() };
        var noseBase = new CanvasPath();
        var baseStroked = false;
        var baseWidth = 0f;
        var slashParts = new List<(CanvasPath Mark, float Alpha)>();
        var noseLine = new CanvasPath();
        var bridgeMark = new CanvasPath();
        var shadowMark = new CanvasPath();
        var hatch = new CanvasPath();
        var sideLine = new CanvasPath();
        var drawn = new List<string>();

        // **Slashes**: the two depressions beside the bridge between the eyes (p. 14), "at least put in one of them".
        // A short concave line each side, bowing in toward the bridge, a little inside the inner eye corner; the far one
        // fades as the head turns, and a light lifts the lit one.
        void Slash(float side, float w)
        {
            var half = Half(side);
            if (half < 0.5f || depressions <= 0f) return;
            float y0 = bridgeTop.Y + (len * 0.04f), y1 = bridgeTop.Y + (len * 0.30f), ym = (y0 + y1) / 2f;
            Point2D a = new(Ridge(y0).X + (side * half * 0.74f), y0), m = new(Ridge(ym).X + (side * half * 0.56f), ym), b = new(Ridge(y1).X + (side * half * 0.70f), y1);
            var cp = ControlThrough(a, m, b);
            var mark = CreateTaperedStrokePath(a, Lerp(a, cp, 2f / 3f), Lerp(b, cp, 2f / 3f), b, Ink(NostrilTier, 0.6f * w) * TaperGain);
            var lit = shadowSide == 0f ? 1f : side == shadowSide ? 1.3f : 0.6f;
            var alpha = Math.Clamp(depressions * lit * MathF.Min(1f, w) * (side == nearSide ? 1f : 0.35f + (0.65f * farRatio)), 0f, 1f);
            if (alpha > 0.01f) slashParts.Add((mark, alpha));
        }

        // **The inner nostril** (p. 14, "concave line of inner nostril"): a dash or a short concave arc, never a round
        // hole. Its length and weight follow the opening's size, which `nostrils` sets.
        void Opening(NoseWing g, bool arc, float side, float w)
        {
            if (g.Rim <= 0.02f) return;
            var rx = g.ROpen * 0.5f * (0.3f + (0.7f * g.Cavity)) * g.OpenX * 1.3f;
            var ry = g.ROpen * 0.24f * (0.6f + (0.4f * g.Cavity)) * g.OpenY;
            CanvasPath mark;
            if (!arc)
            {
                var tilt = g.Inward * (0.2f + (0.9f * under));
                float dx = MathF.Cos(tilt) * rx, dy = MathF.Sin(tilt) * rx;
                Point2D a = new(g.Opening.X - dx, g.Opening.Y - dy), b = new(g.Opening.X + dx, g.Opening.Y + dy);
                mark = CreateTaperedStrokePath(a, new Point2D(Lerp(a, b, 1f / 3f).X, Lerp(a, b, 1f / 3f).Y + (ry * 0.4f)),
                    new Point2D(Lerp(a, b, 2f / 3f).X, Lerp(a, b, 2f / 3f).Y + (ry * 0.4f)), b, ry * 1.6f * w);
            }
            else
            {
                // From under the wing up and in toward the septum, bowing down: concave seen from below.
                Point2D a = new(g.Centre.X + (g.Inward * g.R * 0.35f), g.Centre.Y + (g.R * 0.85f));
                Point2D b = new(g.Opening.X + (g.Inward * rx * 1.1f), g.Opening.Y - (ry * 1.2f));
                Point2D mid = new((a.X + b.X) / 2f, MathF.Max(a.Y, b.Y) + (ry * 0.6f));
                var cp = ControlThrough(a, mid, b);
                mark = CreateTaperedStrokePath(a, Lerp(a, cp, 2f / 3f), Lerp(b, cp, 2f / 3f), b, ry * 1.4f * w);
            }
            openings[side].AddPath(mark);
        }

        // **The nose line** (female 3 and 7, male 4): one stroke down the side of the nose from below the brow, bowed in
        // toward the bridge as the hollow line is, then curling out round its wing.
        void NoseLine(float side, float w)
        {
            var g = Wing(side);
            var half = Half(side);
            float y0 = bridgeTop.Y + (len * 0.10f), ym = bridgeTop.Y + (len * 0.5f);
            Point2D p0 = new(Ridge(y0).X + (side * half * 0.40f), y0), p1 = new(Ridge(ym).X + (side * half * 0.30f), ym);
            Point2D p2 = new(g.Centre.X - (side * g.R * 0.1f), g.Centre.Y - (g.R * 1.15f));
            var pts = SplineThrough([p0, p1, p2], 10);
            if (g.Rim > 0.02f)
                for (var k = 1; k <= 12; k++)
                {
                    var th = (-90f + (k * 190f / 12f)) * MathF.PI / 180f;
                    pts.Add(new Point2D(g.Centre.X + (side * g.R * MathF.Cos(th)), g.Centre.Y + (g.R * MathF.Sin(th))));
                }
            var w0 = Ink(NoseBridgeTier, 0.9f * w);
            noseLine.AddPath(VariableStroke(pts, t => w0 * (0.2f + (0.8f * SmoothStep(0f, 0.55f, t))) * (1f - (0.85f * SmoothStep(0.85f, 1f, t)))));
        }

        // **The profile** (the male semi-front noses): the edge of the front plane on the side the tip swings to, from the
        // end of the nasal bone down, turning round under the tip toward the septum. A front view has no profile, so there
        // the mark is a nose line on the shadow side instead.
        void Profile(float w)
        {
            if (!turned) { NoseLine(markShadow, w); return; }
            var edge = solid.Profile.Path.Points.Select(p => new Point2D(p.X, p.Y)).Concat(solid.ProfileCurl).ToList();
            var w0 = Ink(NoseBridgeTier, 1.1f * w * MathF.Max(0.35f, solid.Turn));
            bridgeMark.AddPath(VariableStroke(SplineThrough(edge, 10), t => w0 * (0.15f + (0.85f * SmoothStep(0f, 0.5f, t))) * (1f - (0.8f * SmoothStep(0.8f, 1f, t)))));
        }

        // A shadow under the wing and the tip on one side (female 4, male 5): the wing in black with a tail under the ball.
        CanvasPath WingShadowShape(float side)
        {
            var g = Wing(side);
            var other = Half(-side);
            var pts = new List<Point2D>();
            for (var k = 0; k <= 14; k++)
            {
                var th = (-115f + (k * 205f / 14f)) * MathF.PI / 180f;
                pts.Add(new Point2D(g.Centre.X + (side * g.R * MathF.Cos(th)), g.Centre.Y + (g.R * MathF.Sin(th))));
            }
            var tail = new Point2D(underNose.X - (side * other * 0.45f), underNose.Y + (len * 0.005f));
            pts.AddRange(SplineThrough([pts[^1], new Point2D(underNose.X + (side * Half(side) * 0.15f), septumPt.Y + (len * 0.02f)), tail], 8).Skip(1));
            pts.AddRange(SplineThrough([tail, new Point2D(underNose.X, underNose.Y - (len * 0.035f)), pts[0]], 8).Skip(1));
            return Polygon(pts);
        }

        // Under the ball between the shadow wing and the septum (female 8): a black wedge, its top the ball's underside.
        CanvasPath UnderShadowShape(float side)
        {
            var g = Wing(side);
            var other = Half(-side);
            return Polygon(SplineThrough(
            [
                new Point2D(g.Centre.X, g.Centre.Y + (g.R * 0.95f)),
                new Point2D(underNose.X - (side * other * 0.25f), septumPt.Y),
                new Point2D(underNose.X + (side * Half(side) * 0.1f), apex.Y + ((underNose.Y - apex.Y) * 0.35f)),
                new Point2D(g.Centre.X + (g.Inward * g.R * 0.9f), g.Centre.Y - (g.R * 0.2f)),
                new Point2D(g.Centre.X, g.Centre.Y + (g.R * 0.95f))
            ], 6));
        }

        // Under the tip on the profile side (male 7, 8): a crescent from where the profile turns under to the septum.
        CanvasPath TipShadowShape()
        {
            var p = profilePage;
            var start = solid.ProfileCurl.Length > 0 ? solid.ProfileCurl[0] : new Point2D(underNose.X + (p * Half(p) * 0.35f), apex.Y + (len * 0.03f));
            var end = new Point2D(underNose.X - (p * Half(-p) * 0.3f), underNose.Y - (len * 0.01f));
            var lower = SplineThrough([start, new Point2D(underNose.X + (p * Half(p) * 0.05f), septumPt.Y + (len * 0.015f)), end], 8);
            var upper = SplineThrough([end, new Point2D(underNose.X + (p * Half(p) * 0.12f), underNose.Y - (len * 0.045f)), start], 8);
            return Polygon(lower.Concat(upper.Skip(1)));
        }

        // Hatching (female 7, male 7): short parallel strokes across a shadow shape, clipped to it.
        void Hatch(CanvasPath region, float w)
        {
            var b = region.Path.TightBounds;
            if (b.IsEmpty) return;
            var step = MathF.Max(1.2f, len * 0.028f);
            var lines = new CanvasPath();
            var thick = Ink(NostrilTier, 0.35f * w);
            for (var x = b.Left - b.Height; x < b.Right; x += step)
            {
                Point2D a = new(x, b.Bottom), c = new(x + (b.Height * 0.6f), b.Top);
                lines.AddPath(VariableStroke([a, c], _ => thick));
            }
            hatch.AddPath(lines.Intersect(region));
        }

        // Seen from below the openings are the subject (p. 15, under view), so a treatment without them gets them.
        if (under > 0.3f && !recipe.Any(m => m.Kind is "nostrils" or "nostrilArcs"))
            recipe = [.. recipe, M("nostrils")];

        if (detail >= 3)
        {
            foreach (var mark in recipe)
            {
                // Tone-bearing marks belong to the finished nose; a detail-3 nose is its lines.
                if (detail < 4 && mark.Kind is "profile" or "shadow" or "underShadow" or "tipShadow" or "hatch") continue;
                var w = mark.Weight;
                var sides = Sides(mark.Side);
                switch (mark.Kind)
                {
                    case "slashes": foreach (var s in sides) Slash(s, w); break;
                    case "wings":
                    case "doubleWings":
                        foreach (var s in sides)
                        {
                            var g = Wing(s);
                            wingArcs[s].AddPath(g.Arc);
                            if (mark.Kind == "doubleWings" && g.Rim > 0.02f)
                            {
                                // A second, shorter arc inside the first: the wing's rolled edge.
                                var inner = new CanvasPath();
                                var outward = g.Inward > 0f ? MathF.PI : 0f;
                                var innerSpan = 55f * MathF.PI / 180f;
                                if (g.Inward < 0f) inner.Arc(g.Centre.X + (g.Inward * g.R * 0.2f), g.Centre.Y, g.R * 0.75f, outward - innerSpan, outward + (innerSpan * 0.6f));
                                else inner.Arc(g.Centre.X + (g.Inward * g.R * 0.2f), g.Centre.Y, g.R * 0.75f, outward + innerSpan, outward - (innerSpan * 0.6f), true);
                                wingArcs[s].AddPath(inner);
                            }
                        }
                        break;
                    case "nostrils":
                    case "nostrilArcs":
                        foreach (var s in sides) Opening(Wing(s), mark.Kind == "nostrilArcs", s, w);
                        break;
                    case "base":
                    {
                        // Hamm's front-view "‿": one line under the tip through the septum, short of the openings.
                        Point2D Toward(Point2D from, float share) => Lerp(from, underNose, share);
                        var baseShare = 0.4f - (0.2f * ballShape);
                        Point2D a = Toward(Wing(-nearSide).Curl, baseShare), b = Toward(Wing(nearSide).Curl, baseShare);
                        // From above the tip overhangs the septum, so the bottom of the ball cups lower.
                        var low = new Point2D(septumPt.X, septumPt.Y + (len * 0.05f * over));
                        var cp = ControlThrough(a, low, b);
                        // As a cubic, so it can square off: a squared ball or a wide septum squared onto the lip pulls the
                        // bottom flat out toward the ends; a rounded ball cups it.
                        Point2D c1 = Lerp(a, cp, 2f / 3f), c2 = Lerp(b, cp, 2f / 3f);
                        var square = Math.Clamp(ballSquare + MathF.Max(0f, septumWidth), -1f, 1f);
                        if (square > 0f) { c1 = new Point2D(c1.X + ((a.X - c1.X) * 0.7f * square), c1.Y); c2 = new Point2D(c2.X + ((b.X - c2.X) * 0.7f * square), c2.Y); }
                        else if (square < 0f)
                        {
                            c1 = new Point2D(c1.X + ((low.X - c1.X) * 0.5f * -square), c1.Y + (len * 0.02f * -square));
                            c2 = new Point2D(c2.X + ((low.X - c2.X) * 0.5f * -square), c2.Y + (len * 0.02f * -square));
                        }
                        noseBase = new CanvasPath();
                        noseBase.MoveTo(a.X, a.Y);
                        noseBase.BezierCurveTo(c1.X, c1.Y, c2.X, c2.Y, b.X, b.Y);
                        baseStroked = true;
                        baseWidth = Ink(NostrilTier, 0.6f * w);
                        break;
                    }
                    case "longBase":
                    {
                        // Female 2: the whole bottom of the nose as one line, wing to wing, the cavities lost in it; the
                        // ends thickened "denoting a little more shadow".
                        Point2D Edge(float s) { var g = Wing(s); return new Point2D(g.Centre.X + (s * g.R * 0.9f), g.Centre.Y - (g.R * 0.35f)); }
                        var w0 = Ink(NostrilTier, 1.1f * w);
                        noseBase = VariableStroke(SplineThrough([Edge(-nearSide), Wing(-nearSide).Curl, septumPt, Wing(nearSide).Curl, Edge(nearSide)], 8),
                            t => w0 * (0.35f + (0.65f * MathF.Pow(MathF.Abs((2f * t) - 1f), 1.2f))));
                        break;
                    }
                    case "waveBase":
                    {
                        // Female 6: a shorter wavy line, heavier in the middle, dipping under each wing.
                        Point2D In(float s, float share, float dy) { var g = Wing(s); var x = underNose.X + ((g.Centre.X - underNose.X) * share); return new Point2D(x, g.Centre.Y + dy); }
                        var r = Wing(nearSide).R;
                        var w0 = Ink(NostrilTier, 1.2f * w);
                        noseBase = VariableStroke(SplineThrough(
                            [In(-nearSide, 1.05f, -r * 0.1f), In(-nearSide, 0.55f, r * 0.75f), new Point2D(underNose.X, septumPt.Y - (r * 0.15f)),
                             In(nearSide, 0.55f, r * 0.75f), In(nearSide, 1.05f, -r * 0.1f)], 8),
                            t => w0 * (0.25f + (0.75f * MathF.Sin(MathF.PI * t))));
                        break;
                    }
                    case "baseBar":
                    {
                        // Male 3: one heavy bar under the tip and almost nothing else.
                        Point2D End(float s) => new(underNose.X + (s * Half(s) * 0.62f), underNose.Y - (len * 0.03f));
                        var w0 = Ink(NoseBridgeTier, 2.6f * w);
                        noseBase = VariableStroke(SplineThrough([End(-nearSide), septumPt, End(nearSide)], 10),
                            t => w0 * (0.2f + (0.8f * MathF.Pow(MathF.Sin(MathF.PI * t), 0.6f))));
                        break;
                    }
                    case "noseLine": foreach (var s in sides) NoseLine(s, w); break;
                    case "profile": Profile(w); break;
                    case "shadow": foreach (var s in sides) shadowMark.AddPath(WingShadowShape(s)); break;
                    case "underShadow": foreach (var s in sides) shadowMark.AddPath(UnderShadowShape(s)); break;
                    case "tipShadow": shadowMark.AddPath(TipShadowShape()); break;
                    case "hatch":
                        foreach (var s in sides)
                            Hatch(mark.Side == "profile" ? TipShadowShape() : WingShadowShape(s), w);
                        break;
                }
                drawn.Add(mark.ToString());
            }
        }

        // **A wide furrowed septum** (p. 15): the furrow, a light line up its middle from the base; the squared corners
        // onto the lip are the base's own (`base` squares off with the septum's width).
        var septumMarks = new CanvasPath();
        if (detail >= 3 && septumWidth > 0.3f)
        {
            var reach = (septumWidth - 0.3f) / 0.7f;
            var top = new Point2D(underNose.X, underNose.Y - (len * 0.07f));
            var foot = new Point2D(underNose.X, septumPt.Y - (len * 0.005f));
            septumMarks.AddPath(CreateTaperedStrokePath(top, Lerp(top, foot, 1f / 3f), Lerp(top, foot, 2f / 3f), foot,
                Ink(NostrilTier, 0.55f * reach) * TaperGain));
        }
        // **The vertical alongside the front-view nose** (p. 14): with the face lit from one side, a line down the side
        // of the nose on the shadow side; in full light treat it lightly, in shadow it cannot be ignored. Left out for a
        // woman's nose, and where the treatment already puts a line on that side.
        if (detail >= 4 && shadowSide != 0f && coarse >= 0f
            && !recipe.Any(m => m.Kind is "noseLine" or "profile" && Sides(m.Side).Contains(shadowSide)))
        {
            var half = Half(shadowSide);
            var g = Wing(shadowSide);
            var y0 = bridgeTop.Y + (len * 0.30f);
            var lineTop = new Point2D(Ridge(y0).X + (shadowSide * half * 0.42f), y0);
            var wingTop = new Point2D(g.Centre.X + (shadowSide * g.R * 0.5f), g.Centre.Y - g.R);
            sideLine.AddPath(CreateTaperedStrokePath(lineTop, Lerp(lineTop, wingTop, 1f / 3f), Lerp(lineTop, wingTop, 2f / 3f), wingTop,
                Ink(NostrilTier, 0.7f) * TaperGain));
        }

        // **How much of the nose to draw** (Hamm's progression, pp. 1, 3, 5): a dash for the base; then the bottom of the
        // ball with one stroke down the side of the bridge; then the treatment's lines; then its tone and shadow shapes
        // over the solid lit. Left out, the level follows the nose's own length; a head 160px tall or more gets all four.
        if (detail <= 2)
        {
            Point2D Out(Point2D edge, float share) => Lerp(underNose, edge, share);
            var markW = Ink(NostrilTier) * TaperGain;
            if (detail == 1)
            {
                var reach = 0.6f * (1f + (0.2f * ballShape));
                Point2D a = Out(farNostril, reach), b = Out(nearNostril, reach);
                var cp = ControlThrough(a, new Point2D(underNose.X, underNose.Y + (len * (0.02f + septumDrop))), b);
                noseBase.AddPath(CreateTaperedStrokePath(a, Lerp(a, cp, 2f / 3f), Lerp(b, cp, 2f / 3f), b, markW));
            }
            else
            {
                var reach = 0.8f * (1f + (0.15f * ballShape));
                Point2D a = Out(farNostril, reach), b = Out(nearNostril, reach);
                a = new Point2D(a.X, a.Y - (len * 0.07f));
                b = new Point2D(b.X, b.Y - (len * 0.07f));
                var low = new Point2D(underNose.X, underNose.Y + (len * (0.02f + septumDrop)));
                noseBase.AddPath(CreateTaperedStrokePath(a,
                    new Point2D(a.X + ((low.X - a.X) * 0.25f), low.Y), new Point2D(b.X + ((low.X - b.X) * 0.25f), low.Y), b, markW));

                // One stroke down the side of the bridge: the shadow side when lit, else the far side.
                var s = shadowSide != 0f ? shadowSide : -nearSide;
                var half = Half(s);
                float y0 = bridgeTop.Y + (len * 0.12f), y1 = bridgeTop.Y + (len * 0.62f);
                Point2D strokeTop = new(Ridge(y0).X + (s * half * 0.62f), y0), strokeEnd = new(Ridge(y1).X + (s * half * 0.5f), y1);
                sideLine.AddPath(CreateTaperedStrokePath(strokeTop, Lerp(strokeTop, strokeEnd, 1f / 3f), Lerp(strokeTop, strokeEnd, 2f / 3f), strokeEnd,
                    Ink(NostrilTier, 0.8f) * TaperGain));
            }
        }

        // What is not drawn at this level is not returned either, so the parts are what is on the page.
        if (detail < 4)
        {
            underPlane = new CanvasPath();
            sideShadow = new CanvasPath();
            facetTones.Clear();
        }

        var depressionMarks = new CanvasPath();
        foreach (var (mark, _) in slashParts) depressionMarks.AddPath(mark);

        ctx.Save();

        // The tone, plane by plane, multiplied so it darkens the skin rather than covering it.
        var toneOptions = new Dictionary<string, object?> { ["color"] = shadowColor, ["softness"] = len * 0.045f };
        if (medium is not null) toneOptions["medium"] = medium;
        foreach (var band in facetTones.Where(t => t.Tone > 0.01f).GroupBy(t => MathF.Round(t.Tone * 8f)))
        {
            toneOptions["amount"] = band.Average(t => t.Tone);
            DrawTone(ctx, MergeNoseFacets(band.Select(t => t.Facet)).Path, toneOptions);
        }

        ApplyMedium(ctx, medium);
        ctx.FillStyle = InMedium(medium, inkColor);
        ctx.Fill(shadowMark);
        ctx.Fill(hatch);
        ctx.Fill(bridgeMark);
        ctx.Fill(noseLine);
        ctx.Fill(septumMarks);

        if (slashParts.Count > 0)
        {
            ctx.Save();
            ctx.FillStyle = InMedium(medium, inkColor, 0.35f);
            foreach (var (mark, alpha) in slashParts)
            {
                ctx.GlobalAlpha = alpha;
                ctx.Fill(mark);
            }
            ctx.Restore();
        }

        if (!sideLine.Path.IsEmpty)
        {
            ctx.Save();
            ctx.FillStyle = InMedium(medium, inkColor, 0.35f);
            ctx.GlobalAlpha = detail <= 2 ? 1f : 0.25f + (0.75f * shade);
            ctx.Fill(sideLine);
            ctx.Restore();
        }

        foreach (var opening in openings.Values) ctx.Fill(opening);

        ctx.StrokeStyle = InMedium(medium, inkColor);
        ctx.LineCap = "round";
        if (baseStroked)
        {
            ctx.LineWidth = baseWidth;
            ctx.Stroke(noseBase);
        }
        else ctx.Fill(noseBase);
        ctx.LineWidth = Ink(NostrilTier);
        foreach (var arc in wingArcs.Values) ctx.Stroke(arc);

        ctx.Restore();

        return new Dictionary<string, object?>
        {
            ["treatment"] = treatment,
            ["marks"] = drawn.Cast<object?>().ToList(),
            ["underPlane"] = underPlane,
            ["bridge"] = bridge,
            ["bridgeMark"] = bridgeMark,
            ["noseLine"] = noseLine,
            ["nostril"] = wingArcs[nearSide],
            ["farNostril"] = wingArcs[-nearSide],
            ["nostrilHole"] = openings[nearSide],
            ["farNostrilHole"] = openings[-nearSide],
            ["base"] = noseBase,
            ["depressions"] = depressionMarks,
            ["shadowMark"] = shadowMark,
            ["septum"] = septumMarks,
            ["variation"] = variation,
            ["hatch"] = hatch,
            ["sideShadow"] = sideShadow,
            ["sideLine"] = sideLine,
            ["planes"] = facetTones.Select(t =>
            {
                var plane = t.Facet.ToDict();
                plane["light"] = t.Light;
                plane["tone"] = t.Tone;
                return (object?)plane;
            }).ToList(),
            ["detail"] = detail
        };
    }
    #endregion
}
