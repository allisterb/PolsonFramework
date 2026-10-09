namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public partial class ConstructiveDrawingToolkit
{
    #region Head Planes
    static readonly string[] HeadPlaneOptions = ["detail", "skull", "face", "yawDeg", "pitchDeg"];

    static readonly string[] HeadPlaneDrawOptions =
        ["light", "lightFront", "values", "color", "amount", "softness", "medium", "lines", "lineColor", "lineWidth", "fill"];

    /// <summary>
    /// The planes of the head (Loomis, <i>Drawing the Head and Hands</i>, Plate 9) as shapes, each with the
    /// direction it faces, so a head can be lit plane by plane.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plate 9: <em>"through them we have a foundation for rendering the head in light and shadow."</em> The basic
    /// planes are the top of the head, the forehead front and its two sides, the eye sockets under the brow, the
    /// nose block (front, sides, bottom), the front of each cheek, the side of the jaw behind the line from the
    /// cheekbone to the chin corner, the muzzle round the mouth and the chin. <c>detail: 'secondary'</c> splits
    /// the forehead, the cheek fronts, the nose's bottom and the muzzle into the facets of his numbered drawing.
    /// </para>
    /// <para>
    /// Every corner is a landmark or a station of the skull outline, so the planes follow an expression, the
    /// character parameters and the turn. The facing directions are the studio's reading of the plate, in the
    /// head's own frame (x toward the near side, y down, z out of the face), turned with the head. Each plane is
    /// clipped to the head's mass.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateHeadPlanes(object headObj, object? options = null)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("createHeadPlanes needs a head from Drawing.createLoomisHead(...) or a call that returns one.",
                nameof(headObj));
        var opt = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(opt, HeadPlaneOptions, "createHeadPlanes option");

        var detail = opt?["detail"]?.ToString()?.Trim().ToLowerInvariant() ?? "basic";
        if (detail is not ("basic" or "secondary"))
            throw new ArgumentException($"createHeadPlanes detail not recognised: {detail}. Accepted: basic, secondary.");

        var geoOptions = new Dictionary<string, object?> { ["neckLength"] = 0f };
        if (opt?["skull"] is { } skull) geoOptions["skull"] = skull;
        if (opt?["face"] is { } face) geoOptions["face"] = face;
        var geo = CreateHeadGeometry(head, geoOptions);
        var mass = (CanvasPath)geo["mass"]!;

        var unit = JsInterop.AsDict(head["unit"]);
        var H = Num(unit, "H", 100f);
        var u = Num(unit, "thirdH", H / 3.5f);
        var eyeW = Num(unit, "eyeW", u * 0.5f);

        Point2D P(string key) => ExtractPoint(head[key]);
        Point2D G(string group, string key) => ExtractPoint(JsInterop.AsDict(head[group])?[key]);
        var crown = P("crown");
        var hairline = P("hairline");
        var brow = P("brow");
        var chin = P("chin");
        var noseBase = P("noseBase");
        var eyeY = Num(head, "eyeLineY", (brow.Y + noseBase.Y) * 0.5f);
        var mouth = JsInterop.AsDict(head["mouthGuides"]);
        var mouthCenter = ExtractPoint(mouth?["center"]);
        var lowerLipY = Num(mouth, "lowerLipY", mouthCenter.Y + (H * 0.03f));

        // The turn: the far eye's width is cos(yaw), and the facial axis swings toward the side the face turns to.
        // Pitch is read back from the brow's drop against the crown, as createLoomisHead lays it down.
        var cosAbs = eyeW > 0f ? Math.Clamp(Num(JsInterop.AsDict(head["farEye"]), "width", eyeW) / eyeW, 0f, 1f) : 1f;
        var faceSign = MathF.Sign(brow.X - crown.X);
        var yaw = opt != null && opt.Contains("yawDeg") && opt["yawDeg"] is not null
            ? Num(opt, "yawDeg", 0f) * MathF.PI / 180f
            : -faceSign * MathF.Acos(cosAbs);
        var pitchY = 2f * ((1.5f * u) - (brow.Y - crown.Y));
        var pitch = opt != null && opt.Contains("pitchDeg") && opt["pitchDeg"] is not null
            ? Num(opt, "pitchDeg", 0f) * MathF.PI / 180f
            : MathF.Asin(Math.Clamp(pitchY / (0.15f * H), -1f, 1f));
        float sy = MathF.Sin(yaw), cy = MathF.Cos(yaw), sp = MathF.Sin(pitch), cp = MathF.Cos(pitch);

        (float X, float Y, float Z) Turn(float x, float y, float z)
        {
            var l = MathF.Sqrt((x * x) + (y * y) + (z * z));
            if (l > 0f) { x /= l; y /= l; z /= l; }
            // Pitch about x (positive nods the face down), then yaw about y (positive turns the face toward -x).
            float y1 = (cp * y) + (sp * z), z1 = (-sp * y) + (cp * z);
            return ((cy * x) - (sy * z1), y1, (sy * x) + (cy * z1));
        }

        // The skull outline's stations, or the jaw frame for a face that has none.
        var stations = JsInterop.AsDict(geo["stations"]);
        var ears = JsInterop.AsDict(geo["ears"]);
        var jaw = JsInterop.AsDict(head["jaw"]);
        Point2D S(string side, string key)
        {
            if (JsInterop.AsDict(stations?[side]) is { } s) return ExtractPoint(s[key]);
            var station = ExtractPoint(jaw?[side == "far" ? "farStation" : "nearStation"]);
            var corner = ExtractPoint(jaw?[side == "far" ? "chinFar" : "chinNear"]);
            return key switch
            {
                "temple" => new Point2D(station.X, brow.Y - (u * 0.3f)),
                "zygoma" => new Point2D(station.X, eyeY + ((noseBase.Y - eyeY) * 0.33f)),
                "gonion" => ExtractPoint(jaw?[side == "far" ? "angle" : "nearAngle"]),
                "chinCorner" => corner,
                "flat" => new Point2D(chin.X + ((corner.X - chin.X) * 0.5f), chin.Y),
                _ => station
            };
        }

        var noseWedge = JsInterop.AsDict(head["noseWedge"]);
        var bridgeTop = ExtractPoint(noseWedge?["bridgeTop"]);
        var apex = ExtractPoint(noseWedge?["apex"]);
        var underNose = ExtractPoint(noseWedge?["underNose"]);
        var axisShift = hairline.X - brow.X;
        var big = H * 2f;

        // The nose's planes are the nose solid's (createNoseSolid), turned by the same yaw and pitch, so the head's
        // planes and the drawn nose agree.
        var noseSolid = noseWedge is null ? null : BuildNoseSolid(noseWedge, 0f, 0f, 0f, yaw, pitch);
        IEnumerable<NoseFacet> NoseFacets(Func<NoseFacet, bool> which) => noseSolid?.Facets.Where(which) ?? [];

        var planes = new List<Dictionary<string, object?>>();
        static CanvasPath Poly(IReadOnlyList<Point2D> pts)
        {
            var poly = new CanvasPath();
            poly.MoveTo(pts[0].X, pts[0].Y);
            for (var i = 1; i < pts.Count; i++) poly.LineTo(pts[i].X, pts[i].Y);
            poly.ClosePath();
            return poly;
        }

        void Add(string name, string group, string side, IReadOnlyList<Point2D> pts, (float X, float Y, float Z) normal) =>
            AddPath(name, group, side, Poly(pts), normal);

        void AddPath(string name, string group, string side, CanvasPath shape, (float X, float Y, float Z) normal)
        {
            var path = shape.Intersect(mass);
            var facing = Turn(normal.X, normal.Y, normal.Z);
            var l = MathF.Sqrt((normal.X * normal.X) + (normal.Y * normal.Y) + (normal.Z * normal.Z));
            planes.Add(new Dictionary<string, object?>
            {
                ["name"] = name,
                ["group"] = group,
                ["side"] = side,
                ["path"] = path,
                ["normal"] = new Dictionary<string, object?> { ["x"] = normal.X / l, ["y"] = normal.Y / l, ["z"] = normal.Z / l },
                ["facing"] = new Dictionary<string, object?> { ["x"] = facing.X, ["y"] = facing.Y, ["z"] = facing.Z },
                // Turned away from the viewer, or clipped to nothing by the outline.
                ["visible"] = facing.Z > 0.02f && !path.IsEmpty
            });
        }

        var secondary = detail == "secondary";
        var sideData = new Dictionary<string, (float D, Point2D FT, Point2D FB, Point2D TopOut)>();
        foreach (var side in new[] { "far", "near" })
        {
            var eye = JsInterop.AsDict(head[side + "Eye"]);
            var browS = JsInterop.AsDict(head[side + "Brow"]);
            Point2D eIn = ExtractPoint(eye?["inner"]), eOut = ExtractPoint(eye?["outer"]), eC = ExtractPoint(eye?["center"]);
            Point2D bIn = ExtractPoint(browS?["inner"]), bOut = ExtractPoint(browS?["outer"]);
            var d = side == "far" ? -1f : 1f;                      // the head-frame side: far is -x
            float dirOut = MathF.Sign(eOut.X - eIn.X);
            if (dirOut == 0f) dirOut = d;

            var zygoma = S(side, "zygoma");
            var gonion = S(side, "gonion");
            var corner = S(side, "chinCorner");
            var flat = S(side, "flat");
            var nostril = G("noseWedge", side + "Nostril");
            var mouthCorner = ExtractPoint(mouth?[side == "far" ? "leftCorner" : "rightCorner"]);

            // Forehead front: from the hairline down to the brow line, between the brows' outer ends.
            var fb = new Point2D(bOut.X, bOut.Y);
            var ft = new Point2D(bOut.X - (dirOut * eyeW * 0.15f) + axisShift, hairline.Y);
            var topOut = new Point2D(ft.X + (dirOut * u * 0.6f), crown.Y - u);
            sideData[side] = (d, ft, fb, topOut);

            // The socket: under the brow ledge, down to the top of the cheekbone.
            var socketOut = new Point2D(eOut.X + (dirOut * eyeW * 0.1f), zygoma.Y);
            var socketIn = new Point2D(eIn.X, eC.Y + ((zygoma.Y - eC.Y) * 0.8f));
            var bridgeSide = new Point2D(bridgeTop.X + (dirOut * eyeW * 0.12f), bridgeTop.Y);
            var apexSide = new Point2D(apex.X + (dirOut * eyeW * 0.22f), apex.Y);

            // The cheek's diagonal from the cheekbone to the chin corner, met at the mouth's level.
            var t = Math.Clamp((mouthCenter.Y - zygoma.Y) / MathF.Max(1f, corner.Y - zygoma.Y), 0f, 1f);
            var cheekLine = Lerp(zygoma, corner, t);
            var chinTop = new Point2D(corner.X, lowerLipY + ((chin.Y - lowerLipY) * 0.35f));

            // The jaw side is the ramus and the masseter: back along the zygomatic arch to the front of the ear,
            // down the back of the ramus to the angle, then forward to the chin corner (Plate 9). Everything
            // outward of the face above and behind it is the side of the head, the ear's own plane.
            var ear = new[] { "far", "near" }
                .Select(k => JsInterop.AsDict(ears?[k]))
                .Where(e => e is not null)
                .MaxBy(e => (ExtractPoint(e!["center"]).X - brow.X) * dirOut);
            var earC = ear is null ? new Point2D(gonion.X, zygoma.Y) : ExtractPoint(ear["center"]);
            var earFrontX = earC.X - (dirOut * (ear is null ? 0f : Num(ear, "width", 0f)) * 0.5f);
            if ((earFrontX - zygoma.X) * dirOut < 0f) earFrontX = zygoma.X;
            var lobeY = earC.Y + ((ear is null ? 0f : Num(ear, "height", 0f)) * 0.4f);
            var jawSide = Poly([zygoma, new Point2D(earFrontX, zygoma.Y), new Point2D(earFrontX, lobeY), gonion,
                                new Point2D(corner.X, chin.Y + big), corner]);
            var headSide = Poly([ft, topOut, new Point2D(topOut.X + (dirOut * big), topOut.Y), new Point2D(topOut.X + (dirOut * big), chin.Y + big),
                                 new Point2D(corner.X, chin.Y + big), corner, zygoma, socketOut, fb]).Subtract(jawSide);
            AddPath($"{side}ForeheadSide", "forehead", side, headSide, (d, -0.1f, 0.1f));   // the sliced side of the ball: sagittal, barely tilted
            Add($"{side}Socket", "eye", side, [bIn, fb, socketOut, socketIn, bridgeSide], (d * 0.15f, 0.6f, 0.8f));
            if (noseSolid is null)
                Add($"{side}NoseSide", "nose", side, [bridgeSide, apexSide, nostril, socketIn], (d * 0.9f, 0f, 0.45f));
            else
            {
                var (sidePath, sideNormal) = MergeNoseFacets(NoseFacets(f => f.Side == side && f.Group is "side" or "wing"));
                AddPath($"{side}NoseSide", "nose", side, sidePath, (sideNormal.X, sideNormal.Y, sideNormal.Z));
            }
            AddPath($"{side}JawSide", "cheek", side, jawSide, (d * 0.9f, 0.25f, 0.35f));

            if (secondary)
            {
                // Plate 9's numbered cheek: 3 beside the nose, 2 under the eye facing up, 1 low and outward.
                var q = new Point2D(nostril.X + ((zygoma.X - nostril.X) * 0.45f), (socketIn.Y + nostril.Y) * 0.5f);
                Add($"{side}CheekUpper", "cheek", side, [socketIn, socketOut, zygoma, q], (d * 0.3f, -0.35f, 0.9f));
                Add($"{side}CheekInner", "cheek", side, [socketIn, q, nostril], (d * 0.45f, -0.1f, 0.9f));
                Add($"{side}CheekLower", "cheek", side, [q, zygoma, cheekLine, nostril], (d * 0.4f, 0.2f, 0.9f));
                Add($"{side}UpperLip", "muzzle", side,
                    [nostril, underNose, new Point2D(mouthCenter.X, Num(mouth, "upperLipY", mouthCenter.Y)), mouthCorner, cheekLine],
                    (d * 0.4f, -0.1f, 0.9f));
            }
            else
            {
                Add($"{side}CheekFront", "cheek", side, [socketIn, socketOut, zygoma, cheekLine, nostril], (d * 0.35f, 0.05f, 1f));
            }

            sideData[side + "Low"] = (d, cheekLine, chinTop, corner);
            sideData[side + "Chin"] = (d, flat, nostril, mouthCorner);
        }

        var (_, ftFar, fbFar, topFar) = sideData["far"];
        var (_, ftNear, fbNear, topNear) = sideData["near"];
        var (_, lineFar, chinTopFar, cornerFar) = sideData["farLow"];
        var (_, lineNear, chinTopNear, cornerNear) = sideData["nearLow"];
        var (_, flatFar, nostrilFar, mouthFar) = sideData["farChin"];
        var (_, flatNear, nostrilNear, mouthNear) = sideData["nearChin"];

        Add("top", "forehead", "center", [ftFar, ftNear, topNear, new Point2D(topNear.X, crown.Y - big), new Point2D(topFar.X, crown.Y - big), topFar],
            (0f, -1f, 0.35f));
        if (secondary)
        {
            // The forehead in three facets, the middle one facing straight out.
            Point2D A(float s) => Lerp(ftFar, ftNear, s);
            Point2D B(float s) => Lerp(fbFar, fbNear, s);
            Add("farForehead", "forehead", "far", [A(0f), A(0.33f), B(0.33f), B(0f)], (-0.35f, -0.25f, 1f));
            Add("forehead", "forehead", "center", [A(0.33f), A(0.67f), B(0.67f), B(0.33f)], (0f, -0.25f, 1f));
            Add("nearForehead", "forehead", "near", [A(0.67f), A(1f), B(1f), B(0.67f)], (0.35f, -0.25f, 1f));
        }
        else
        {
            Add("forehead", "forehead", "center", [ftFar, ftNear, fbNear, fbFar], (0f, -0.25f, 1f));
        }

        void AddNose(string name, string side, Func<NoseFacet, bool> which)
        {
            var (path, n) = MergeNoseFacets(NoseFacets(which));
            AddPath(name, "nose", side, path, (n.X, n.Y, n.Z));
        }

        if (noseSolid is null)
        {
            var bridgeFar = new Point2D(bridgeTop.X - (eyeW * 0.12f), bridgeTop.Y);
            var bridgeNear = new Point2D(bridgeTop.X + (eyeW * 0.12f), bridgeTop.Y);
            var apexFar = new Point2D(apex.X - (eyeW * 0.22f), apex.Y);
            var apexNear = new Point2D(apex.X + (eyeW * 0.22f), apex.Y);
            Add("noseFront", "nose", "center", [bridgeFar, bridgeNear, apexNear, apexFar], (0f, -0.3f, 1f));
            Add("noseBottom", "nose", "center", [apexFar, apexNear, nostrilNear, underNose, nostrilFar], (0f, 1f, 0.25f));
        }
        else
        {
            AddNose("noseFront", "center", f => f.Group is "bridge" or "ball");
            if (secondary)
            {
                AddNose("farNoseBottom", "far", f => f.Group == "under" && f.Side == "far");
                AddNose("noseBottom", "center", f => f.Group == "under" && f.Side == "center");
                AddNose("nearNoseBottom", "near", f => f.Group == "under" && f.Side == "near");
            }
            else AddNose("noseBottom", "center", f => f.Group == "under");
        }

        if (secondary)
        {
            Add("lowerLip", "muzzle", "center", [lineFar, mouthFar, mouthCenter, mouthNear, lineNear, chinTopNear, chinTopFar], (0f, 0.2f, 1f));
        }
        else
        {
            Add("muzzle", "muzzle", "center", [nostrilFar, underNose, nostrilNear, lineNear, chinTopNear, chinTopFar, lineFar], (0f, 0.05f, 1f));
        }

        Add("chin", "chin", "center", [chinTopFar, chinTopNear, cornerNear, flatNear, flatFar, cornerFar], (0f, 0.25f, 1f));

        return new Dictionary<string, object?>
        {
            ["planes"] = planes.Cast<object?>().ToList(),
            ["byName"] = planes.ToDictionary(p => (string)p["name"]!, p => (object?)p),
            ["mass"] = mass,
            ["detail"] = detail,
            ["yawDeg"] = yaw * 180f / MathF.PI,
            ["pitchDeg"] = pitch * 180f / MathF.PI
        };
    }

    /// <summary>
    /// Lights the planes of a head: each takes the tone its facing earns against a light, in Loomis's three
    /// values (light, halftone, shadow) or graded.
    /// </summary>
    /// <remarks>
    /// <c>light</c> is the direction toward the light on the page in degrees (0 right, −90 up), as for
    /// <c>drawComicNose</c>; <c>lightFront</c> (0–1, default 0.5) is how far it comes from in front. A plane
    /// facing the light more than 0.45 is left as paper, one facing it more than 0.1 takes half the tone, and the
    /// rest take all of it; <c>values: 2</c> keeps only light and shadow, and <c>values: 0</c> grades smoothly.
    /// The thresholds are the studio's. Pass a head instead of planes and its basic planes are built.
    /// </remarks>
    public Dictionary<string, object?> DrawHeadPlanes(CanvasRenderingContext2D ctx, object planesOrHead, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(opt, HeadPlaneDrawOptions, "drawHeadPlanes option");

        var given = JsInterop.AsDict(planesOrHead)
                    ?? throw new ArgumentException("drawHeadPlanes needs planes from Drawing.createHeadPlanes(...) or a head.");
        var set = given.Contains("planes") ? given : CreateHeadPlanes(planesOrHead);
        if (set["planes"] is not IEnumerable list) throw new ArgumentException("drawHeadPlanes: these planes carry no list of planes.");

        var angle = Num(opt, "light", -135f) * MathF.PI / 180f;
        var front = Math.Clamp(Num(opt, "lightFront", 0.5f), 0f, 1f);
        var side = MathF.Sqrt(1f - (front * front));
        (float X, float Y, float Z) light = (MathF.Cos(angle) * side, MathF.Sin(angle) * side, front);
        var values = (int)Num(opt, "values", 3f);
        if (values is not (0 or 2 or 3)) throw new ArgumentException($"drawHeadPlanes values must be 0 (graded), 2 or 3, not {values}.");
        var amount = Math.Clamp(Num(opt, "amount", 0.4f), 0f, 1f);
        var fill = opt?["fill"] is not bool f || f;
        var lines = opt?["lines"] is true;

        var toneOptions = new Dictionary<string, object?> { ["color"] = opt?["color"]?.ToString() ?? "#2a2a2a" };
        if (opt?["softness"] is { } softness) toneOptions["softness"] = softness;
        if (opt?["medium"] is { } medium) toneOptions["medium"] = medium;

        var lit = new List<object?>();
        foreach (var item in list)
        {
            if (JsInterop.AsDict(item) is not IDictionary plane || plane["path"] is not CanvasPath path) continue;
            if (plane["visible"] is false) continue;
            var n = JsInterop.AsDict(plane["facing"]);
            var dot = (Num(n, "x", 0f) * light.X) + (Num(n, "y", 0f) * light.Y) + (Num(n, "z", 1f) * light.Z);
            var tone = values switch
            {
                0 => amount * Math.Clamp(1f - dot, 0f, 1f),
                2 => dot > 0.1f ? 0f : amount,
                _ => dot > 0.45f ? 0f : dot > 0.1f ? amount * 0.45f : amount
            };

            if (fill && tone > 0.001f)
            {
                toneOptions["amount"] = tone;
                DrawTone(ctx, path, toneOptions);
            }

            if (lines)
            {
                ctx.Save();
                ctx.StrokeStyle = opt?["lineColor"]?.ToString() ?? "#4a90e2";
                ctx.LineWidth = Num(opt, "lineWidth", 1f);
                ctx.Stroke(path);
                ctx.Restore();
            }

            lit.Add(new Dictionary<string, object?> { ["name"] = plane["name"], ["light"] = dot, ["tone"] = tone });
        }

        return new Dictionary<string, object?> { ["planes"] = lit, ["light"] = new Dictionary<string, object?>
            { ["x"] = light.X, ["y"] = light.Y, ["z"] = light.Z } };
    }
    #endregion
}
