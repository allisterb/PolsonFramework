namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// Hamm's eye wheel (<i>Drawing the Head and Figure</i>, p. 10) in <c>eyeWheel</c>: five horizontals with numbered settings,
/// 3 the normal awake setting, that move up and down, twist and tilt; and the three areas of skin where lines appear.
/// </summary>
public class EyeWheelTests : TestsRuntime
{
    static readonly ConstructiveDrawingToolkit Toolkit = new();

    static Dictionary<string, object?> Head() => Toolkit.CreateLoomisHead(400f, 400f, 560f, 0f);

    static Dictionary<string, object?> Wheel(Dictionary<string, object?> head, Dictionary<string, object?> settings) => Toolkit.EyeWheel(head, settings);

    static Dictionary<string, object?> Part(Dictionary<string, object?> head, string key) => (Dictionary<string, object?>)head[key]!;

    static float Y(Dictionary<string, object?> group, string point) => Convert.ToSingle(((Dictionary<string, object?>)group[point]!)["y"]);

    static SkiaSharp.SKRect Eye(Dictionary<string, object?> head, string part = "aperture") =>
        ((CanvasPath)Toolkit.DrawComicEye(new SkiaCanvas(800, 800).GetContext("2d"), head["nearEye"]!, false, null)[part]!).Path.TightBounds;

    [Fact]
    public void TestTheNormalSettingChangesNothing()
    {
        var head = Head();
        var normal = Wheel(head, new());
        foreach (var key in new[] { "nearEye", "farEye", "nearBrow", "farBrow" })
            foreach (var (k, v) in Part(head, key))
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(v), System.Text.Json.JsonSerializer.Serialize(Part(normal, key)[k]));
        Assert.Equal(Eye(head), Eye(normal));
        var lines = (Dictionary<string, object?>)Part(normal, "eyeLines")["near"]!;
        Assert.All(new[] { "A", "B", "C" }, a => Assert.Equal(0f, Convert.ToSingle(lines[a])));
    }

    /// <summary>The two lids move on their own: the upper lid opens and closes over a lower lid that stays put, and the
    /// lower lid rises over the iris under an upper lid that stays put.</summary>
    [Fact]
    public void TestTheLidsMoveOnTheirOwn()
    {
        var head = Head();
        var plain = Eye(head);
        var wide = Eye(Wheel(head, new() { ["upperLid"] = 1f }));
        var shut = Eye(Wheel(head, new() { ["upperLid"] = 5f }));
        Assert.True(wide.Top < plain.Top - 3f && shut.Top > plain.Top + 3f);
        Assert.Equal(plain.Bottom, wide.Bottom, 1f);
        Assert.Equal(plain.Bottom, shut.Bottom, 1f);

        var raised = Eye(Wheel(head, new() { ["lowerLid"] = 1f }));
        Assert.True(raised.Bottom < plain.Bottom - 3f, "the lower lid pushed up");
        Assert.Equal(plain.Top, raised.Top, 1f);
    }

    /// <summary>
    /// The fold rises with its setting as far as the brow allows: with the brows raised it goes up; with them at their
    /// normal it cannot, because on this construction the normal fold already sits just under the brow.
    /// </summary>
    [Fact]
    public void TestTheFoldRisesWithItsSetting()
    {
        var head = Head();
        var raisedBrows = new Dictionary<string, object?> { ["browTop"] = 1f, ["browBottom"] = 1f };
        var withBrows = Eye(Wheel(head, new(raisedBrows)), "fold");
        var withFold = Eye(Wheel(head, new(raisedBrows) { ["fold"] = 1f }), "fold");
        Assert.True(withFold.Top < withBrows.Top - 3f, "fold 1 rises under raised brows");
        Assert.True(Eye(Wheel(head, new() { ["fold"] = 1f }), "fold").Top >= Eye(head, "fold").Top - 0.5f, "and never past a brow left at its normal");
    }

    /// <summary>A brow edge set at one end only tilts the brow; a side set on its own twists the face.</summary>
    [Fact]
    public void TestTheBrowsTiltAndTwist()
    {
        var head = Head();
        var worry = Wheel(head, new() { ["browTop"] = new Dictionary<string, object?> { ["inner"] = 1f, ["outer"] = 3f } });
        Assert.True(Y(Part(worry, "nearBrow"), "inner") < Y(Part(head, "nearBrow"), "inner") - 2f);
        Assert.Equal(Y(Part(head, "nearBrow"), "outer"), Y(Part(worry, "nearBrow"), "outer"), 0.5f);

        var frown = Wheel(head, new() { ["browBottom"] = new Dictionary<string, object?> { ["inner"] = 5f } });
        Assert.True(Y(Part(frown, "nearBrow"), "inner") > Y(Part(head, "nearBrow"), "inner") + 2f);

        var twist = Wheel(head, new() { ["far"] = new Dictionary<string, object?> { ["browTop"] = 1f, ["browBottom"] = 1f } });
        Assert.True(Y(Part(twist, "farBrow"), "peak") < Y(Part(head, "farBrow"), "peak") - 4f);
        Assert.Equal(Y(Part(head, "nearBrow"), "peak"), Y(Part(twist, "nearBrow"), "peak"), 0.01f);
    }

    /// <summary>The lines appear where the settings make them, and drawEyeLines draws them there.</summary>
    [Fact]
    public void TestTheSkinLinesFollowTheSettings()
    {
        var head = Head();
        static float Line(Dictionary<string, object?> h, string area) =>
            Convert.ToSingle(((Dictionary<string, object?>)((Dictionary<string, object?>)h["eyeLines"]!)["near"]!)[area]);
        Dictionary<string, object?> Drawn(Dictionary<string, object?> h) => Toolkit.DrawEyeLines(new SkiaCanvas(800, 800).GetContext("2d"), h);
        static bool Has(Dictionary<string, object?> parts, string key) => !((CanvasPath)parts[key]!).Path.IsEmpty;

        var frown = Wheel(head, new() { ["browBottom"] = new Dictionary<string, object?> { ["inner"] = 5f } });
        Assert.Equal(1f, Line(frown, "A"));
        Assert.True(Has(Drawn(frown), "between") && !Has(Drawn(frown), "forehead"));

        var raised = Wheel(head, new() { ["browTop"] = 1f });
        Assert.Equal(1f, Line(raised, "B"));
        var forehead = ((CanvasPath)Drawn(raised)["forehead"]!).Path.TightBounds;
        Assert.True(forehead.Bottom < Y(Part(raised, "nearBrow"), "peak"), "the forehead lines are above the brows");

        var laugh = Wheel(head, new() { ["lowerLid"] = 1f });
        Assert.Equal(1f, Line(laugh, "C"));
        var feet = ((CanvasPath)Drawn(laugh)["outer"]!).Path.TightBounds;
        float OuterX(string eye) => Convert.ToSingle(((Dictionary<string, object?>)Part(laugh, eye)["outer"]!)["x"]);
        Assert.True(feet.Right > OuterX("nearEye") + 2f && feet.Left < OuterX("farEye") - 2f, "beyond both outer corners");

        Assert.Equal(0.5f, Line(Wheel(head, new() { ["lines"] = new Dictionary<string, object?> { ["C"] = 0.5f } }), "C"));
        Assert.False(Has(Drawn(head), "between"), "a head without settings has no lines");
        Assert.True(Has(Toolkit.DrawEyeLines(new SkiaCanvas(800, 800).GetContext("2d"), head, new Dictionary<string, object?> { ["B"] = 1f }), "forehead"));
    }

    /// <summary>
    /// Nothing the wheel adds may read as a second brow: the forehead lines start well up the forehead rather than
    /// hugging the brow's arch, and a raised fold stays under the brow's lower edge.
    /// </summary>
    [Fact]
    public void TestNothingReadsAsASecondBrow()
    {
        var head = Head();
        var w = Convert.ToSingle(Part(head, "nearEye")["width"]);
        foreach (var settings in new Dictionary<string, object?>[]
                 {
                     new() { ["browTop"] = 1f, ["browBottom"] = 2f, ["fold"] = 1f, ["upperLid"] = 1f },
                     new() { ["browTop"] = new Dictionary<string, object?> { ["inner"] = 3f, ["outer"] = 2.5f } },
                     new() { ["browTop"] = new Dictionary<string, object?> { ["inner"] = 1f, ["outer"] = 3f } }
                 })
        {
            var wheeled = Wheel(head, settings);
            var brow = Part(wheeled, "nearBrow");
            var th = Convert.ToSingle(brow["thickness"]);
            var browTop = MathF.Min(Y(brow, "inner"), MathF.Min(Y(brow, "peak"), Y(brow, "outer"))) - (th / 2f);
            var lines = ((CanvasPath)Toolkit.DrawEyeLines(new SkiaCanvas(800, 800).GetContext("2d"), wheeled)["forehead"]!).Path;
            if (!lines.IsEmpty) Assert.True(lines.TightBounds.Bottom < browTop - (w * 0.25f), "forehead lines clear of the brow");

            var fold = ((CanvasPath)Toolkit.DrawComicEye(new SkiaCanvas(800, 800).GetContext("2d"), wheeled["nearEye"]!, false, null)["fold"]!).Path.TightBounds;
            Assert.True(fold.Top > Y(brow, "peak") + (th / 2f), "the fold stays under the brow");
        }
    }

    [Fact]
    public void TestSettingsOutsideTheWheelAreRefused()
    {
        var head = Head();
        Assert.Throws<ArgumentException>(() => Wheel(head, new() { ["upperLid"] = 6f }));
        Assert.Throws<ArgumentException>(() => Wheel(head, new() { ["browTop"] = 4f }));
        Assert.Throws<ArgumentException>(() => Wheel(head, new() { ["eyelid"] = 2f }));
        Assert.Throws<ArgumentException>(() => Wheel(head, new() { ["browTop"] = new Dictionary<string, object?> { ["middle"] = 2f } }));
        Assert.Throws<ArgumentException>(() => Wheel(head, new() { ["near"] = new Dictionary<string, object?> { ["far"] = new Dictionary<string, object?>() } }));
    }

    /// <summary>A wheel setting is a head, so it blends: halfway to a raised fold is half the raise.</summary>
    [Fact]
    public void TestAWheelHeadBlends()
    {
        var head = Head();
        var wide = Wheel(head, new() { ["fold"] = 1f, ["upperLid"] = 2f });
        var half = Toolkit.BlendHead(head, head, wide, 0.5f);
        float Fold(Dictionary<string, object?> h) => Convert.ToSingle(Part(h, "nearEye")["foldLift"]);
        Assert.Equal(Fold(wide) / 2f, Fold(half), 0.01f);
    }
}
