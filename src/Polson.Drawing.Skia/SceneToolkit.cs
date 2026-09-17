namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// A staged composition as a construction: depth layers, named slots, and the colour system that
/// holds them together.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// <b>This is the second creation route, and it is a different trade rather than a better one.</b>
/// Everywhere else the studio constructs a picture from geometry — a head from landmarks, a figure
/// from a canon. Here the picture is <i>arranged</i>: the parts arrive from somewhere else, and what
/// the toolkit supplies is where they go, how deep they sit, and what colour holds them together.
/// Measured across every run on disk, figure construction is not slower than composition at the
/// median but carries roughly twice the tail — so this buys out the variance, at the cost of
/// articulation, which is the same trade a human makes between a model sheet and a redraw.
/// </para>
/// <para>
/// The formula is distilled from Lex Sokolin, <i>How I Built Procedurally Generated Gothic Comic
/// Compositions</i> (medium.muz.li, 15 January 2019), and stated in our own words; see the ledger
/// row in <c>reference/README.md</c> and Studio Manual 20.
/// </para>
/// <para>
/// <b>Nothing here draws.</b> It returns a model, on the same split as
/// <see cref="ConstructiveDrawingToolkit.CreateMannequinFigure"/> against
/// <c>CreateFigureGeometry</c> and as <see cref="ChartToolkit"/> against its geometry — closed-form
/// arithmetic, no paths allocated, so a hundred candidate arrangements can be measured for the cost
/// of one.
/// </para>
/// </remarks>
public class SceneToolkit
{
    #region Methods
    /// <summary>
    /// A composition in three depth layers, with a slot for each thing that goes in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The formula, and every part of it earns its place.</b> The frame divides into horizontal
    /// thirds and splices into three sequential depth layers. The <b>background</b> holds a texture
    /// mass in the upper reaches and a <b>diagonal</b> that runs against the figure's own lean — it
    /// is what stops a centred figure reading as a portrait, and it carries the mood colour. The
    /// <b>figure</b> sits low, in the bottom two thirds, leaving the top band for a title and for
    /// atmosphere, and it is meant to be the brightest thing in the frame. The <b>foreground</b> is
    /// an object or a decoration that pulls the eye and then hands it to the figure, and it carries
    /// the mood colour a second time so the figure is held between two uses of it.
    /// </para>
    /// <para>
    /// <b>Pass an <c>rng</c> to vary it.</b> Without one the arrangement is the canonical placement
    /// and is identical every call; with one, every slot is jittered within the bounds stated by
    /// <c>variation</c>. The generator is drawn from in a fixed order and a fixed number of times, so
    /// the same seed gives the same arrangement — which is the whole reason
    /// <see cref="RandomToolkit"/> exists and the reason a procedural composition can be re-rendered
    /// from its own record at all.
    /// </para>
    /// <para>
    /// <b><c>order</c> is depth here, unlike everywhere else in the toolkit.</b> On a figure and on a
    /// head it is construction order and explicitly not z; a staged scene is layers by definition, so
    /// painting in <c>order</c> is correct and is the point.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateLayeredScene(object rect, object? options = null)
    {
        var frame = LayoutToolkit.AsRect(rect);
        var opt = JsInterop.AsDict(options);
        Refuse(opt, LayeredSceneOptions, "createLayeredScene option");

        var rng = JsInterop.AsDict(options) is { } o && o["rng"] is SeededRandom given ? given : null;
        var variation = Clamp(Num(opt, "variation", rng is null ? 0f : 1f), 0f, 1f);

        // Read in a fixed order so the same seed always gives the same scene. Every draw happens
        // whether or not variation is zero, so turning variation down never shifts the sequence.
        var jitterTitle = Jitter(rng);
        var jitterFigure = Jitter(rng);
        var jitterWidth = Jitter(rng);
        var jitterDiagonal = Jitter(rng);
        var jitterFore = Jitter(rng);
        var flipped = rng is not null && rng.Bool(0.5) && variation > 0f;

        var third = frame.Height / 3f;
        var thirds = new List<object?>
        {
            LayoutToolkit.Make(frame.X, frame.Y, frame.Width, third),
            LayoutToolkit.Make(frame.X, frame.Y + third, frame.Width, third),
            LayoutToolkit.Make(frame.X, frame.Y + third * 2f, frame.Width, third)
        };

        // The title band is reserved out of the top third; the figure never enters it.
        var titleShare = Clamp(Num(opt, "titleBand", 0.22f) + jitterTitle * 0.05f * variation, 0f, 0.6f);
        var title = LayoutToolkit.Make(frame.X, frame.Y, frame.Width, frame.Height * titleShare);

        // The figure occupies the bottom two thirds, sized as a share of the frame's height.
        var figureShare = Clamp(Num(opt, "figureHeight", 0.62f) + jitterFigure * 0.08f * variation, 0.15f, 0.95f);
        var figureHeight = frame.Height * figureShare;
        var figureWidth = figureHeight * Clamp(Num(opt, "figureAspect", 0.52f) + jitterWidth * 0.06f * variation, 0.15f, 2f);
        var figureBase = frame.Y + frame.Height * Clamp(Num(opt, "figureBase", 0.94f), 0.3f, 1f);
        var figureCx = frame.X + frame.Width * Clamp(0.5f + jitterWidth * 0.10f * variation, 0.15f, 0.85f);
        var figure = LayoutToolkit.Make(figureCx - figureWidth / 2f, figureBase - figureHeight, figureWidth, figureHeight);

        // The texture mass fills the upper two thirds behind everything.
        var texture = LayoutToolkit.Make(frame.X, frame.Y, frame.Width, third * 2f);

        // The diagonal leans against the figure, so a figure placed left gets a diagonal from the
        // right. That opposition is the whole reason it is in the formula.
        var lean = figureCx < frame.X + frame.Width / 2f ? 1f : -1f;
        if (flipped) lean = -lean;

        var angle = Clamp(Num(opt, "diagonalDeg", 38f) + jitterDiagonal * 12f * variation, 8f, 80f);
        // **It lives in the upper two thirds, and that is not a stylistic preference.** Run from the
        // figure's feet it is occluded along most of its length by the figure itself, and the element
        // whose job is to cross behind the composition survives as two stubs - measured at 58% visible
        // before this. Fixed fractions were not enough either: a figure that jitters wide swallows it
        // again, at 57% on one seed in forty. So the low end is derived from the figure's own
        // rectangle - just outside it, a fifth of the way down - and the overlap is bounded by
        // construction rather than by luck.
        var margin = frame.Width * 0.05f;
        var startX = lean > 0f
            ? Math.Min(frame.X + frame.Width * 0.94f, Convert.ToSingle(figure["x2"]) + margin)
            : Math.Max(frame.X + frame.Width * 0.06f, Convert.ToSingle(figure["x"]) - margin);
        var startY = Convert.ToSingle(figure["y"]) + figureHeight * 0.18f;
        var endX = frame.X + frame.Width * (lean > 0f ? 0.30f : 0.70f);
        var diagonal = new Dictionary<string, object?>
        {
            ["from"] = Point(startX, startY),
            ["to"] = Point(endX, frame.Y + frame.Height * 0.02f),
            ["angleDeg"] = angle * lean,
            ["lean"] = lean,
            ["width"] = frame.Width * 0.14f,
            ["band"] = texture
        };

        // The foreground sits on the bottom edge and overlaps the figure's feet, which is what it is
        // for: it hides the contact and passes the eye upward.
        var foreShare = Clamp(Num(opt, "foregroundHeight", 0.18f) + jitterFore * 0.05f * variation, 0.02f, 0.5f);
        var foreHeight = frame.Height * foreShare;
        var foreground = LayoutToolkit.Make(frame.X, frame.Y + frame.Height - foreHeight, frame.Width, foreHeight);

        var slots = new List<object?>
        {
            Slot("texture", "background", texture, 0),
            Slot("diagonal", "background", RectOf(diagonal, frame), 1),
            Slot("figure", "middle", figure, 2),
            Slot("foreground", "foreground", foreground, 3),
            Slot("title", "foreground", title, 4)
        };

        return new Dictionary<string, object?>
        {
            ["bounds"] = LayoutToolkit.Make(frame.X, frame.Y, frame.Width, frame.Height),
            ["thirds"] = thirds,
            ["layers"] = new Dictionary<string, object?>
            {
                ["background"] = new Dictionary<string, object?> { ["band"] = texture, ["texture"] = texture, ["diagonal"] = diagonal },
                ["middle"] = new Dictionary<string, object?> { ["band"] = thirds[1], ["figure"] = figure, ["baseline"] = figureBase },
                ["foreground"] = new Dictionary<string, object?> { ["band"] = foreground, ["object"] = foreground, ["title"] = title }
            },
            ["slots"] = slots,
            ["figure"] = figure,
            ["title"] = title,
            ["flipped"] = flipped,
            ["variation"] = variation,
            ["seed"] = rng is null ? null : rng.Seed,
            // Depth, not construction order - see the remarks. Paint in this sequence.
            ["order"] = new List<object?> { "texture", "diagonal", "figure", "foreground", "title" }
        };
    }

    /// <summary>
    /// A mood palette: one hue for the figure, a different one for the ground it is held between.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three values per hue, and the placement rules are the substance rather than the colours.</b>
    /// The figure carries its hue's <c>highlight</c> and is the brightest thing in the frame; the
    /// <b>mood</b> hue is a different one and appears twice, on the background diagonal and again in
    /// the foreground, so the figure is held between two uses of it. Everything else starts at
    /// <c>ground</c>, which is near-black: a composition that begins dark and has colour cut into it
    /// reads differently from one that begins light and has colour added.
    /// </para>
    /// <para>
    /// <b>The five default hues carry no blue and no purple</b>, which is Sokolin's selection rather
    /// than a rule of colour — it is what makes the set read as one world. <b>They are tuned for a
    /// cover</b>, so a quiet daylight interior is outside their range: pass <c>hues</c> as
    /// <c>{ name: { dark, mid, highlight } }</c> to replace them entirely, with at least two names,
    /// since the whole rule is that the figure's hue and the mood's differ. The placement rules are
    /// what this call is actually for.
    /// </para>
    /// <para>
    /// <b>The shape is documented in the reference because a live run could not find it there and
    /// bypassed the call</b>, hand-writing its palette instead — correct on a short clock, and the one
    /// thing the generator exists to supply.
    /// </para>
    /// <para>
    /// Distinct from <c>Drawing.createNotanPalette</c>, which is tonal: that answers how light and
    /// dark are distributed, this answers which hue goes where.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateMoodPalette(object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        Refuse(opt, MoodPaletteOptions, "createMoodPalette option");

        var rng = opt is { } o && o["rng"] is SeededRandom given ? given : null;
        var hues = ReadHues(opt) ?? DefaultHues();
        if (hues.Count < 2)
        {
            throw new ArgumentException(
                "createMoodPalette needs at least two hues: the figure and the mood have to differ, "
                + "which is the rule the whole palette rests on.");
        }

        var names = hues.Keys.ToArray();
        var figureIndex = Index(opt, "figure", names, rng);

        // The mood hue must differ from the figure's. Drawn from the remaining names rather than
        // re-rolled until it differs, so the number of draws does not depend on luck.
        var others = names.Where((_, i) => i != figureIndex).ToArray();
        var moodName = opt is not null && opt.Contains("mood") && opt["mood"] is not null
            ? Named(opt["mood"]!.ToString(), names, "mood")
            : others[rng is null ? 0 : rng.Int(0, others.Length)];

        if (string.Equals(moodName, names[figureIndex], StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"createMoodPalette: figure and mood are both '{moodName}'. The figure has to stand "
                + "against the ground it is held between, so they must differ.");
        }

        var figureName = names[figureIndex];
        return new Dictionary<string, object?>
        {
            ["ground"] = opt?["ground"]?.ToString() ?? "#0b0b10",
            ["figureName"] = figureName,
            ["moodName"] = moodName,
            ["figure"] = hues[figureName],
            ["mood"] = hues[moodName],
            ["hues"] = hues.ToDictionary(h => h.Key, h => (object?)h.Value),
            ["names"] = new List<object?>(names),
            ["seed"] = rng is null ? null : rng.Seed
        };
    }

    /// <summary>
    /// A space, described once, so every panel of a sequence is a view of the same place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A storyboard is one space seen across time, and that is what this exists to make
    /// structural.</b> <see cref="CreateLayeredScene"/> composes a single frame and gives each panel
    /// an <i>independent</i> arrangement, which is the opposite of continuity — right for a cover,
    /// wrong for a sequence. Here the room is described once and the camera moves over it, so a table
    /// cannot quietly relocate between panels: there is only one table.
    /// </para>
    /// <para>
    /// **Coordinates are the set's own**, not the page's. Give the space whatever extent reads
    /// conveniently — 100 × 60 is a reasonable habit — and place elements in it; a
    /// <see cref="CreateShot"/> maps that into a panel rectangle. Nothing here is perspective: a shot
    /// is a crop and a scale, which is what a storyboard needs and is the whole reason this stays
    /// closed-form arithmetic.
    /// </para>
    /// <para>
    /// <c>horizon</c> is where the floor meets the wall, as a fraction of the set's height. The
    /// <c>wall</c> and <c>floor</c> rectangles fall out of it, and are the two masses almost every
    /// interior needs before anything else is placed.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateSet(object rect, object? options = null)
    {
        var bounds = LayoutToolkit.AsRect(rect);
        var opt = JsInterop.AsDict(options);
        Refuse(opt, SetOptions, "createSet option");

        var horizon = Clamp(Num(opt, "horizon", 0.55f), 0.02f, 0.98f);
        var floorY = bounds.Y + bounds.Height * horizon;

        var named = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var list = new List<object?>();

        if (JsInterop.AsDict(opt?["elements"]) is IDictionary || opt?["elements"] is IEnumerable)
        {
            foreach (var entry in Items(opt?["elements"]))
            {
                if (JsInterop.AsDict(entry) is not IDictionary e)
                {
                    throw new ArgumentException(
                        "createSet: every element must be a { name, x, y, width, height } object.");
                }

                var name = e["name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("createSet: every element needs a name, so a shot can focus on it.");

                // **Refused rather than last-wins.** Two elements sharing a name means one of them is
                // unreachable by `focusOn` and silently absent from `shot.elements`, which reads as a
                // drawing bug rather than as a naming one.
                if (named.ContainsKey(name))
                    throw new ArgumentException($"createSet: two elements are named '{name}'. Names have to be unique.");

                var box = LayoutToolkit.Make(Num(e, "x", 0f), Num(e, "y", 0f),
                    MathF.Max(0f, Num(e, "width", 0f)), MathF.Max(0f, Num(e, "height", 0f)));
                box["name"] = name;
                named[name] = box;
                list.Add(box);
            }
        }

        return new Dictionary<string, object?>
        {
            ["bounds"] = LayoutToolkit.Make(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            ["horizon"] = horizon,
            ["floorY"] = floorY,
            ["wall"] = LayoutToolkit.Make(bounds.X, bounds.Y, bounds.Width, floorY - bounds.Y),
            ["floor"] = LayoutToolkit.Make(bounds.X, floorY, bounds.Width, bounds.Y + bounds.Height - floorY),
            ["elements"] = named,
            ["list"] = list
        };
    }

    /// <summary>
    /// One panel's view of a set: a distance from the camera to the subject, and what it frames.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A shot is a crop and a scale of the same space, never a redrawing of it.</b> That is what
    /// makes continuity structural rather than a thing to remember: the wall, the window and the
    /// table are the set's, and a shot only decides how much of them is on screen and how large.
    /// </para>
    /// <para>
    /// The ladder is Janson's, closest to widest — <c>extremeCloseUp</c>, <c>closeUp</c>,
    /// <c>medium</c>, <c>full</c>, <c>long</c>, <c>extremeLong</c>, and <c>establishing</c>, which is
    /// wide because its job is to fix <i>where</i>. Studio Manual 20 §2 has what each is for. Pass
    /// <c>coverage</c> to override the fraction of the set's width a named shot frames.
    /// </para>
    /// <para>
    /// <b>A shot is a crop, so it cannot turn around.</b> The set describes what the camera faces; a
    /// <i>reverse angle</i> looks at the wall behind it, which the set does not contain and no crop of
    /// it can produce. Shot-reverse-shot is the basic grammar of two people talking, so this is the
    /// limit most likely to be met — found by a live run rather than foreseen here. Describe both
    /// walls as two sets, or draw the reverse by hand and record that the panel is not a view of the
    /// set.
    /// </para>
    /// <para>
    /// <b>The view is clamped inside the set</b>, so a camera cannot pan off into space nobody
    /// described. A focus near an edge gives a frame pressed against that edge rather than a frame
    /// half full of nothing — which is the same decision a person makes at a drawing board.
    /// </para>
    /// <para>
    /// <c>backgroundDetail</c> carries the manual's rule rather than leaving it in prose: <i>the
    /// closer the camera, the less background you should draw</i>. It reads <c>none</c> at the two
    /// closest rungs, where Janson says flat black or white beats detail, and <c>full</c> at the
    /// widest, where the location is the subject.
    /// </para>
    /// </remarks>
    public Shot CreateShot(object setObj, object panelRect, object? options = null)
    {
        if (JsInterop.AsDict(setObj) is not IDictionary set || set["bounds"] is null)
            throw new ArgumentException("createShot needs a set from Scene.createSet(...).", nameof(setObj));

        var opt = JsInterop.AsDict(options);
        Refuse(opt, ShotOptions, "createShot option");

        var bounds = LayoutToolkit.AsRect(set["bounds"]!);
        var panel = LayoutToolkit.AsRect(panelRect);
        if (panel.Width <= 0f || panel.Height <= 0f)
            throw new ArgumentException("createShot: the panel has no extent to frame into.");

        var name = opt?["shot"]?.ToString()?.Trim() ?? "medium";
        var key = ShotLadder.Keys.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"createShot shot not recognised: {name}. Accepted: {string.Join(", ", ShotLadder.Keys)}.");

        var coverage = Clamp(Num(opt, "coverage", ShotLadder[key]), 0.02f, 1f);

        // The frame takes the panel's aspect and is shrunk, never stretched, to stay inside the set.
        var viewW = bounds.Width * coverage;
        var viewH = viewW * (panel.Height / panel.Width);
        if (viewH > bounds.Height)
        {
            viewH = bounds.Height;
            viewW = viewH * (panel.Width / panel.Height);
        }

        if (viewW > bounds.Width)
        {
            viewW = bounds.Width;
            viewH = viewW * (panel.Height / panel.Width);
        }

        var focus = Focus(set, opt, bounds);
        var viewX = Clamp(focus.X - viewW / 2f, bounds.X, MathF.Max(bounds.X, bounds.X + bounds.Width - viewW));
        var viewY = Clamp(focus.Y - viewH / 2f, bounds.Y, MathF.Max(bounds.Y, bounds.Y + bounds.Height - viewH));

        return new Shot(key, coverage, BackgroundDetail(key),
            LayoutToolkit.Make(viewX, viewY, viewW, viewH),
            LayoutToolkit.Make(panel.X, panel.Y, panel.Width, panel.Height),
            JsInterop.AsDict(set["elements"]) as IDictionary);
    }
    #endregion

    #region Methods (private)
    static Dictionary<string, object?> Slot(string name, string layer, object? rect, int depth) =>
        new()
        {
            ["name"] = name,
            ["layer"] = layer,
            ["rect"] = rect,
            ["depth"] = depth
        };

    static Dictionary<string, object?> Point(float x, float y) => new() { ["x"] = x, ["y"] = y };

    /// <summary>Where the camera is aimed: a named element, an explicit point, or the set's centre.</summary>
    static (float X, float Y) Focus(IDictionary set, IDictionary? opt, LayoutToolkit.RectValue bounds)
    {
        if (JsInterop.AsDict(opt?["subjectAt"]) is IDictionary at)
            return (Num(at, "x", bounds.X + bounds.Width / 2f), Num(at, "y", bounds.Y + bounds.Height / 2f));

        if (opt is not null && opt.Contains("focusOn") && opt["focusOn"] is not null)
        {
            var wanted = opt["focusOn"]!.ToString();
            if (JsInterop.AsDict(set["elements"]) is IDictionary elements)
            {
                foreach (DictionaryEntry entry in elements)
                {
                    if (!string.Equals(entry.Key?.ToString(), wanted, StringComparison.OrdinalIgnoreCase)) continue;
                    var box = JsInterop.AsDict(entry.Value)!;
                    return (Num(box, "cx", 0f), Num(box, "cy", 0f));
                }
            }

            var names = JsInterop.AsDict(set["elements"]) is IDictionary all
                ? string.Join(", ", all.Keys.Cast<object>().Select(k => k?.ToString()))
                : "(none)";
            throw new ArgumentException(
                $"createShot focusOn not recognised: {wanted}. The set has: {names}.");
        }

        return (bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
    }

    /// <summary>Manual 20 2: the closer the camera, the less background there should be.</summary>
    static string BackgroundDetail(string shot) => shot switch
    {
        "extremeCloseUp" or "closeUp" => "none",
        "medium" => "minimal",
        "full" => "some",
        _ => "full"
    };

    /// <summary>Any enumerable as a list; a single value counts as one item.</summary>
    static List<object?> Items(object? items)
    {
        var list = new List<object?>();
        if (items is IEnumerable sequence and not string)
        {
            foreach (var item in sequence)
            {
                // A JS array reaches here as entries; a dictionary of elements reaches here as pairs.
                list.Add(item is DictionaryEntry pair ? pair.Value : item);
            }
        }
        else if (items is not null)
        {
            list.Add(items);
        }

        return list;
    }

    /// <summary>The diagonal's own extent, so it has a rectangle like every other slot.</summary>
    static Dictionary<string, object> RectOf(Dictionary<string, object?> diagonal, LayoutToolkit.RectValue frame)
    {
        var from = (Dictionary<string, object?>)diagonal["from"]!;
        var to = (Dictionary<string, object?>)diagonal["to"]!;
        var x0 = Math.Min(Convert.ToSingle(from["x"]), Convert.ToSingle(to["x"]));
        var x1 = Math.Max(Convert.ToSingle(from["x"]), Convert.ToSingle(to["x"]));
        var y0 = Math.Min(Convert.ToSingle(from["y"]), Convert.ToSingle(to["y"]));
        var y1 = Math.Max(Convert.ToSingle(from["y"]), Convert.ToSingle(to["y"]));
        return LayoutToolkit.Make(x0, y0, Math.Max(1f, x1 - x0), Math.Max(1f, y1 - y0));
    }

    /// <summary>One signed jitter in <c>[-1, 1]</c>, or zero when there is no generator.</summary>
    /// <remarks>
    /// Always called, generator or not, so that <c>variation: 0</c> and a missing <c>rng</c> are the
    /// canonical arrangement and turning variation down never shifts the sequence.
    /// </remarks>
    static float Jitter(SeededRandom? rng) => rng is null ? 0f : (float)(rng.Next() * 2.0 - 1.0);

    static int Index(IDictionary? opt, string key, string[] names, SeededRandom? rng)
    {
        if (opt is not null && opt.Contains(key) && opt[key] is not null)
        {
            var wanted = Named(opt[key]!.ToString(), names, key);
            return Array.FindIndex(names, n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase));
        }

        return rng is null ? 0 : rng.Int(0, names.Length);
    }

    static string Named(string? wanted, string[] names, string key)
    {
        var match = names.FirstOrDefault(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ArgumentException(
            $"createMoodPalette {key} not recognised: {wanted}. Accepted: {string.Join(", ", names)}.");
    }

    /// <summary>The caller's hues, each as dark / mid / highlight.</summary>
    static Dictionary<string, Dictionary<string, object?>>? ReadHues(IDictionary? opt)
    {
        if (JsInterop.AsDict(opt?["hues"]) is not IDictionary given) return null;

        var hues = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in given)
        {
            var name = entry.Key?.ToString();
            if (name is null) continue;

            if (JsInterop.AsDict(entry.Value) is not IDictionary values)
            {
                throw new ArgumentException(
                    $"createMoodPalette hue '{name}' is not a {{ dark, mid, highlight }} object.");
            }

            hues[name] = new Dictionary<string, object?>
            {
                ["dark"] = Text(values, "dark"),
                ["mid"] = Text(values, "mid"),
                ["highlight"] = Text(values, "highlight")
            };
        }

        return hues.Count == 0 ? null : hues;
    }

    /// <summary>
    /// The five hues of the source: red, orange, yellow, teal, green — no blue and no purple.
    /// </summary>
    static Dictionary<string, Dictionary<string, object?>> DefaultHues() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["red"] = Values("#3d0f11", "#8c1c1c", "#d6402f"),
            ["orange"] = Values("#3a1c08", "#9c5510", "#e08427"),
            ["yellow"] = Values("#3a300a", "#9a8319", "#e5c33c"),
            ["teal"] = Values("#0b2b2b", "#1b6360", "#3fa39c"),
            ["green"] = Values("#152a12", "#3f6b2c", "#7aa64a")
        };

    static Dictionary<string, object?> Values(string dark, string mid, string highlight) =>
        new() { ["dark"] = dark, ["mid"] = mid, ["highlight"] = highlight };

    static string? Text(IDictionary values, string key) =>
        values.Contains(key) ? values[key]?.ToString() : null;

    static float Num(IDictionary? d, string key, float fallback) =>
        d != null && d.Contains(key) && d[key] != null
            ? Convert.ToSingle(d[key], CultureInfo.InvariantCulture)
            : fallback;

    static float Clamp(float value, float min, float max) =>
        float.IsFinite(value) ? Math.Min(max, Math.Max(min, value)) : min;

    /// <summary>Refuses a misspelled option by name rather than silently drawing the canon.</summary>
    static void Refuse(IDictionary? opt, string[] accepted, string noun)
    {
        if (opt is null) return;

        var unknown = new List<string>();
        foreach (DictionaryEntry entry in opt)
        {
            var name = entry.Key?.ToString();
            if (name is not null && !accepted.Contains(name, StringComparer.OrdinalIgnoreCase)) unknown.Add(name);
        }

        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"{noun}{(unknown.Count > 1 ? "s" : string.Empty)} not recognised: "
                + $"{string.Join(", ", unknown)}. Accepted: {string.Join(", ", accepted)}.");
        }
    }
    #endregion

    #region Fields
    static readonly string[] LayeredSceneOptions =
        ["rng", "variation", "titleBand", "figureHeight", "figureAspect", "figureBase",
         "diagonalDeg", "foregroundHeight"];

    static readonly string[] MoodPaletteOptions = ["rng", "hues", "figure", "mood", "ground"];

    static readonly string[] SetOptions = ["horizon", "elements"];

    static readonly string[] ShotOptions = ["shot", "coverage", "focusOn", "subjectAt"];

    /// <summary>
    /// Janson's ladder, closest to widest, as the fraction of the set's width each one frames.
    /// </summary>
    /// <remarks>
    /// <c>establishing</c> sits at the wide end because its job is to fix <i>where</i>, which is a
    /// purpose rather than a distance — Manual 20 §2 lists it beside the others for that reason and
    /// so does this.
    /// </remarks>
    static readonly Dictionary<string, float> ShotLadder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["extremeCloseUp"] = 0.10f,
        ["closeUp"] = 0.18f,
        ["medium"] = 0.35f,
        ["full"] = 0.55f,
        ["long"] = 0.80f,
        ["extremeLong"] = 1.00f,
        ["establishing"] = 1.00f
    };
    #endregion
}

/// <summary>One panel's view of a set: what is framed, how large, and how much background to draw.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class Shot
{
    #region Constructors
    internal Shot(string name, float coverage, string backgroundDetail,
        Dictionary<string, object> view, Dictionary<string, object> panel, IDictionary? elements)
    {
        Name = name;
        Coverage = coverage;
        BackgroundDetail = backgroundDetail;
        View = view;
        Panel = panel;

        Scale = Convert.ToSingle(panel["width"]) / MathF.Max(0.0001f, Convert.ToSingle(view["width"]));
        this.elements = elements;
    }
    #endregion

    #region Properties
    /// <summary>The rung of the ladder this is, as given.</summary>
    public string Name { get; }

    /// <summary>The fraction of the set's width framed.</summary>
    public float Coverage { get; }

    /// <summary>How much background to draw: <c>none</c>, <c>minimal</c>, <c>some</c> or <c>full</c>.</summary>
    public string BackgroundDetail { get; }

    /// <summary>The part of the set that is on screen, in the set's own coordinates.</summary>
    public Dictionary<string, object> View { get; }

    /// <summary>The panel this is drawn into, in page coordinates.</summary>
    public Dictionary<string, object> Panel { get; }

    /// <summary>Set units to page pixels.</summary>
    public float Scale { get; }
    #endregion

    #region Methods
    /// <summary>A point in the set, in page coordinates.</summary>
    public Dictionary<string, object?> Point(float x, float y) => new()
    {
        ["x"] = Convert.ToSingle(Panel["x"]) + (x - Convert.ToSingle(View["x"])) * Scale,
        ["y"] = Convert.ToSingle(Panel["y"]) + (y - Convert.ToSingle(View["y"])) * Scale
    };

    /// <summary>A rectangle in the set, in page coordinates.</summary>
    public Dictionary<string, object> Place(object rect)
    {
        var r = LayoutToolkit.AsRect(rect);
        var origin = Point(r.X, r.Y);
        return LayoutToolkit.Make(Convert.ToSingle(origin["x"]), Convert.ToSingle(origin["y"]),
            r.Width * Scale, r.Height * Scale);
    }

    /// <summary>A named element of the set, in page coordinates, or <c>null</c> when the set has no such element.</summary>
    /// <remarks>
    /// Null rather than an exception, because asking whether a shot can see something is an ordinary
    /// question a panel loop asks about every element in turn. Use <see cref="Shows"/> for the
    /// visibility half.
    /// </remarks>
    public Dictionary<string, object>? Element(string name)
    {
        var box = Find(name);
        return box is null ? null : Place(box);
    }

    /// <summary>Whether any part of a named element falls inside this frame.</summary>
    public bool Shows(string name)
    {
        var box = Find(name);
        if (box is null) return false;

        var r = LayoutToolkit.AsRect(box);
        var vx = Convert.ToSingle(View["x"]);
        var vy = Convert.ToSingle(View["y"]);
        var vw = Convert.ToSingle(View["width"]);
        var vh = Convert.ToSingle(View["height"]);

        return r.X < vx + vw && r.X + r.Width > vx && r.Y < vy + vh && r.Y + r.Height > vy;
    }
    #endregion

    #region Methods (private)
    IDictionary? Find(string name)
    {
        if (elements is null) return null;
        foreach (DictionaryEntry entry in elements)
        {
            if (string.Equals(entry.Key?.ToString(), name, StringComparison.OrdinalIgnoreCase))
                return JsInterop.AsDict(entry.Value);
        }

        return null;
    }
    #endregion

    #region Fields
    readonly IDictionary? elements;
    #endregion
}
