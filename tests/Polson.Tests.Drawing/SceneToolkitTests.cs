namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>Scene</c> — the staged composition, and the colour system that holds it together.
///
/// **What is under test is the formula and its reproducibility, not whether the picture is good.**
/// A staged arrangement fails in ways a render does not announce: a diagonal leaning the same way as
/// the figure reads as a portrait rather than a composition, a figure that strays into the title band
/// collides with type nobody has drawn yet, and an arrangement that cannot be reproduced from its
/// seed cannot be discussed at all.
/// </summary>
public class SceneToolkitTests : TestsRuntime
{
    static SceneToolkit Kit => new();
    static RandomToolkit Rng => new();

    static Dictionary<string, object> Frame(float w = 640f, float h = 900f) =>
        new LayoutToolkit().Rect(0f, 0f, w, h);

    static Dictionary<string, object?> Scene(object? options = null) =>
        Kit.CreateLayeredScene(Frame(), options);

    static Dictionary<string, object?> Sub(Dictionary<string, object?> d, string key) =>
        (Dictionary<string, object?>)d[key]!;

    static float Num(object? d, string key) =>
        Convert.ToSingle(((IDictionary<string, object>)d!)[key]);

    static float Of(Dictionary<string, object?> d, string key) => Convert.ToSingle(d[key]!);

    static Dictionary<string, object?>[] Slots(Dictionary<string, object?> scene) =>
        [.. ((List<object?>)scene["slots"]!).Cast<Dictionary<string, object?>>()];

    /// <summary>Every slot in the formula is present, in painting order.</summary>
    [Fact]
    public void TestTheFormulaIsComplete()
    {
        var scene = Scene();
        var names = Slots(scene).Select(s => s["name"]!.ToString()).ToArray();

        Assert.Equal(["texture", "diagonal", "figure", "foreground", "title"], names);
        Assert.Equal(names, ((List<object?>)scene["order"]!).Select(o => o!.ToString()));

        // Depth rises monotonically, because here `order` really is z.
        var depths = Slots(scene).Select(s => Convert.ToInt32(s["depth"]!)).ToArray();
        Assert.Equal(depths.OrderBy(d => d), depths);
    }

    /// <summary>
    /// The figure sits low and clear of the title band.
    /// </summary>
    /// <remarks>
    /// The band is reserved for type and atmosphere. A figure that runs into it collides with
    /// lettering nobody has drawn yet, so the collision surfaces late — in a finished render, against
    /// text the agent added last.
    /// </remarks>
    [Fact]
    public void TestTheFigureStaysBelowTheTitleBand()
    {
        foreach (var seed in new[] { 1, 7, 1907, 44_000 })
        {
            var scene = Scene(new Dictionary<string, object?> { ["rng"] = Rng.Seeded(seed) });
            var figure = Sub(scene, "figure");
            var title = Sub(scene, "title");

            Assert.True(Of(figure, "y") >= Of(title, "y2"),
                $"seed {seed}: figure top {Of(figure, "y"):F1} is inside the title band ending {Of(title, "y2"):F1}");

            // And it stands on the bottom two thirds rather than floating in the middle of the frame.
            Assert.True(Of(figure, "y2") > 900f * 0.66f, $"seed {seed}: the figure does not reach the lower third");
        }
    }

    /// <summary>
    /// The diagonal leans against the figure, which is the formula rather than decoration.
    /// </summary>
    /// <remarks>
    /// A diagonal running the same way as the figure's placement reinforces it instead of opposing
    /// it, and the composition reads as a centred portrait with a stripe on it. This is the one rule
    /// in the scene that a reader feels immediately and that no measurement of the parts would catch.
    /// </remarks>
    [Fact]
    public void TestTheDiagonalLeansAgainstTheFigure()
    {
        var tested = 0;
        foreach (var seed in Enumerable.Range(1, 40))
        {
            var scene = Scene(new Dictionary<string, object?> { ["rng"] = Rng.Seeded(seed) });
            if (Convert.ToBoolean(scene["flipped"]!)) continue;      // a flip is a deliberate reversal

            var figure = Sub(scene, "figure");
            var diagonal = Sub(Sub(Sub(scene, "layers"), "background"), "diagonal");
            var lean = Convert.ToSingle(diagonal["lean"]!);
            var offCentre = Of(figure, "cx") - 320f;

            if (Math.Abs(offCentre) < 1f) continue;
            tested++;
            Assert.True(offCentre * lean <= 0f,
                $"seed {seed}: figure is {(offCentre < 0 ? "left" : "right")} of centre and the diagonal leans the same way");
        }

        Assert.True(tested > 5, $"only {tested} seeds actually placed the figure off centre");
    }

    /// <summary>
    /// The diagonal is mostly visible, rather than hidden behind the figure.
    /// </summary>
    /// <remarks>
    /// **Found by rendering, which is why it is a test.** The first arrangement ran the diagonal from
    /// the figure's feet to the top of the frame, and the figure occluded it along **42%** of its
    /// length — so the one element whose job is to cross behind the composition survived as two stubs
    /// at either end, and the picture read as a centred block with a stripe above it. Nothing in the
    /// model was wrong and every other test passed. Raising it into the upper two thirds took the
    /// visible share to over 80%.
    /// </remarks>
    [Fact]
    public void TestTheDiagonalIsNotSwallowedByTheFigure()
    {
        foreach (var seed in Enumerable.Range(1, 40))
        {
            var scene = Scene(new Dictionary<string, object?> { ["rng"] = Rng.Seeded(seed) });
            var figure = Sub(scene, "figure");
            var diagonal = Sub(Sub(Sub(scene, "layers"), "background"), "diagonal");
            var from = Sub(diagonal, "from");
            var to = Sub(diagonal, "to");

            var outside = 0;
            const int samples = 200;
            for (var i = 0; i <= samples; i++)
            {
                var t = (float)i / samples;
                var x = Of(from, "x") + (Of(to, "x") - Of(from, "x")) * t;
                var y = Of(from, "y") + (Of(to, "y") - Of(from, "y")) * t;
                var hidden = x >= Of(figure, "x") && x <= Of(figure, "x2")
                          && y >= Of(figure, "y") && y <= Of(figure, "y2");
                if (!hidden) outside++;
            }

            var visible = (float)outside / (samples + 1);
            Assert.True(visible > 0.6f,
                $"seed {seed}: only {visible:P0} of the diagonal clears the figure");
        }
    }

    /// <summary>The same seed gives the same scene; a different one gives a different scene.</summary>
    [Fact]
    public void TestTheSceneIsReproducibleFromItsSeed()
    {
        static string Shape(Dictionary<string, object?> scene) =>
            string.Join("|", Slots(scene).Select(s =>
            {
                var r = (Dictionary<string, object>)s["rect"]!;
                return $"{s["name"]}:{Convert.ToSingle(r["x"]):F3},{Convert.ToSingle(r["y"]):F3},"
                     + $"{Convert.ToSingle(r["width"]):F3},{Convert.ToSingle(r["height"]):F3}";
            }));

        var a = Shape(Scene(new Dictionary<string, object?> { ["rng"] = Rng.Seeded(1907) }));
        var b = Shape(Scene(new Dictionary<string, object?> { ["rng"] = Rng.Seeded(1907) }));
        var c = Shape(Scene(new Dictionary<string, object?> { ["rng"] = Rng.Seeded(1908) }));

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    /// <summary>
    /// Turning variation down gives the canon, and does not shift the sequence.
    /// </summary>
    /// <remarks>
    /// The generator is read a fixed number of times whatever `variation` is, so a caller can tune
    /// how much randomness a scene carries without losing the arrangement they had. Drawing only when
    /// variation is non-zero would make the two knobs interact, which is the commonest way a
    /// procedural system becomes impossible to tune.
    /// </remarks>
    [Fact]
    public void TestVariationZeroIsTheCanonAndCostsTheSameDraws()
    {
        var quiet = Rng.Seeded(1907);
        var loud = Rng.Seeded(1907);

        var canon = Scene(new Dictionary<string, object?> { ["rng"] = quiet, ["variation"] = 0f });
        var varied = Scene(new Dictionary<string, object?> { ["rng"] = loud, ["variation"] = 1f });

        Assert.Equal(quiet.Count, loud.Count);
        Assert.Equal(Of(Sub(Scene(), "figure"), "x"), Of(Sub(canon, "figure"), "x"), 3);
        Assert.NotEqual(Of(Sub(canon, "figure"), "x"), Of(Sub(varied, "figure"), "x"), 3);
    }

    /// <summary>Without a generator the arrangement is the canon, every time.</summary>
    [Fact]
    public void TestNoGeneratorMeansNoVariation()
    {
        var a = Sub(Scene(), "figure");
        var b = Sub(Scene(), "figure");

        Assert.Equal(Of(a, "x"), Of(b, "x"), 4);
        Assert.Equal(0f, Convert.ToSingle(Scene()["variation"]!));
        Assert.Null(Scene()["seed"]);
    }

    /// <summary>A misspelled option is refused by name.</summary>
    [Fact]
    public void TestAnUnknownSceneOptionIsRefused()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Scene(new Dictionary<string, object?> { ["figureHieght"] = 0.5f }));

        Assert.Contains("figureHieght", ex.Message);
        Assert.Contains("figureHeight", ex.Message);
    }

    /// <summary>The palette gives three values per hue and the figure a different hue from the mood.</summary>
    [Fact]
    public void TestTheMoodHueDiffersFromTheFigure()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var mood = Kit.CreateMoodPalette(new Dictionary<string, object?> { ["rng"] = Rng.Seeded(seed) });
            Assert.NotEqual(mood["figureName"]!.ToString(), mood["moodName"]!.ToString());

            foreach (var key in new[] { "figure", "mood" })
            {
                var hue = Sub(mood, key);
                Assert.All(new[] { "dark", "mid", "highlight" }, v => Assert.False(string.IsNullOrWhiteSpace(hue[v]?.ToString())));
            }
        }
    }

    /// <summary>Asking for the same hue twice is refused, rather than drawn as a figure on itself.</summary>
    [Fact]
    public void TestAskingForOneHueTwiceIsRefused()
    {
        var ex = Assert.Throws<ArgumentException>(() => Kit.CreateMoodPalette(
            new Dictionary<string, object?> { ["figure"] = "red", ["mood"] = "red" }));

        Assert.Contains("must differ", ex.Message);
    }

    /// <summary>A named hue is honoured; an unknown one is refused by name.</summary>
    [Fact]
    public void TestHuesAreNamed()
    {
        var mood = Kit.CreateMoodPalette(new Dictionary<string, object?> { ["figure"] = "teal" });
        Assert.Equal("teal", mood["figureName"]!.ToString());

        var ex = Assert.Throws<ArgumentException>(() => Kit.CreateMoodPalette(
            new Dictionary<string, object?> { ["figure"] = "mauve" }));
        Assert.Contains("mauve", ex.Message);
        Assert.Contains("teal", ex.Message);
    }

    /// <summary>The caller's own hues replace the defaults entirely.</summary>
    [Fact]
    public void TestSuppliedHuesReplaceTheDefaults()
    {
        var hues = new Dictionary<string, object?>
        {
            ["ink"] = new Dictionary<string, object?> { ["dark"] = "#111", ["mid"] = "#444", ["highlight"] = "#eee" },
            ["rust"] = new Dictionary<string, object?> { ["dark"] = "#311", ["mid"] = "#833", ["highlight"] = "#e55" }
        };

        var mood = Kit.CreateMoodPalette(new Dictionary<string, object?> { ["hues"] = hues, ["figure"] = "ink" });

        Assert.Equal("ink", mood["figureName"]!.ToString());
        Assert.Equal("rust", mood["moodName"]!.ToString());
        Assert.Equal(["ink", "rust"], ((List<object?>)mood["names"]!).Select(n => n!.ToString()));
    }

    /// <summary>One hue cannot make a palette, because the whole rule is that two differ.</summary>
    [Fact]
    public void TestOneHueIsRefused()
    {
        var hues = new Dictionary<string, object?>
        {
            ["only"] = new Dictionary<string, object?> { ["dark"] = "#111", ["mid"] = "#444", ["highlight"] = "#eee" }
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            Kit.CreateMoodPalette(new Dictionary<string, object?> { ["hues"] = hues }));

        Assert.Contains("at least two", ex.Message);
    }

    /// <summary>The default set carries no blue and no purple — the source's selection, stated.</summary>
    [Fact]
    public void TestTheDefaultHuesAreTheSourcesFive()
    {
        var names = ((List<object?>)Kit.CreateMoodPalette()["names"]!).Select(n => n!.ToString()).ToArray();

        Assert.Equal(["red", "orange", "yellow", "teal", "green"], names);
        Assert.DoesNotContain("blue", names);
        Assert.DoesNotContain("purple", names);
    }
}
