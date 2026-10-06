namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// The mannequin's builds: Loomis's female canon (*Figure Drawing for All It's Worth* p. 27) and his
/// proportions at various ages (p. 29), with Gautier's child face (*1,001 Faces*, ch. "Children").
/// </summary>
public class FigureBuildTests : TestsRuntime
{
    const float T = 800f, OY = 100f;

    static readonly ConstructiveDrawingToolkit Toolkit = new();

    static Dictionary<string, object?> Figure(Dictionary<string, object?>? options = null) =>
        Toolkit.CreateMannequinFigure(400f, OY, T, options);

    static Dictionary<string, object?> D(Dictionary<string, object?> d, string key) => (Dictionary<string, object?>)d[key]!;

    static float F(Dictionary<string, object?> d, string key) => Convert.ToSingle(d[key]);

    static float Y(Dictionary<string, object?> d, string key) => F(D(d, key), "y");

    /// <summary>The fraction of the height down from the crown.</summary>
    static float Down(Dictionary<string, object?> fig, float y) => (y - OY) / T;

    #region Male and female
    [Fact]
    public void TestTheMaleBuildIsTheFigureAsBefore()
    {
        var plain = Figure();
        var male = Figure(new() { ["build"] = "male" });
        Assert.Equal(T / 8f, F(plain, "headUnit"), 3);
        Assert.Equal(0.5f, Down(plain, Y(plain, "crotch")), 4);
        foreach (var key in new[] { "navel", "crotch", "neck", "sternum" })
            Assert.Equal(Y(plain, key), Y(male, key), 3);
        Assert.Equal(F(D(plain, "bounds"), "width"), F(D(male, "bounds"), "width"), 2);
        Assert.Equal(T / 8f, F(D(plain, "build"), "widthUnit"), 3);
    }

    [Fact]
    public void TestTheFemaleNavelAndCrotchSitLowerThanTheMales()
    {
        var male = Figure();
        var female = Figure(new() { ["build"] = "female" });
        Assert.Equal(8f, F(D(female, "build"), "heads"), 3);
        Assert.Equal(13f / 24f, Down(female, Y(female, "crotch")), 3);       // a third of a head below the middle
        Assert.Equal(19f / 48f, Down(female, Y(female, "navel")), 3);        // a sixth of a head below the male's
        Assert.Equal(Y(female, "crotch"), Y(D(female, "leftArm"), "wrist"), 2);  // wrists level with the crotch
        Assert.True(Y(female, "navel") > Y(male, "navel"));
    }

    [Fact]
    public void TestTheFemaleIsNarrowerAtTheShouldersAndWiderAtTheHips()
    {
        float Across(Dictionary<string, object?> fig, string group, string a, string b) =>
            MathF.Abs(F(D(D(fig, group), b), "x") - F(D(D(fig, group), a), "x"));
        var options = new Dictionary<string, object?> { ["shoulderTiltDeg"] = 0f, ["pelvicTiltDeg"] = 0f };
        var male = Figure(options);
        var female = Figure(new(options) { ["build"] = "female" });

        Assert.True(Across(female, "clavicles", "left", "right") < Across(male, "clavicles", "left", "right"));
        Assert.True(F(D(female, "pelvis"), "rx") > F(D(male, "pelvis"), "rx"));
    }
    #endregion

    #region Ages
    [Theory]
    [InlineData(1f, 4f)]
    [InlineData(3f, 5f)]
    [InlineData(5f, 6f)]
    [InlineData(10f, 7f)]
    [InlineData(15f, 7.5f)]
    [InlineData(18f, 8f)]
    [InlineData(40f, 8f)]
    [InlineData(0f, 4f)]
    public void TestAChildIsLoomisHeadsTallForItsAge(float age, float heads)
    {
        var fig = Figure(new() { ["age"] = age });
        Assert.Equal(heads, F(D(fig, "build"), "heads"), 2);
        Assert.Equal(T / heads, F(fig, "headUnit"), 1);
        Assert.Equal(OY + T, Y(D(fig, "leftLeg"), "foot"), 2);
    }

    [Fact]
    public void TestAnAgeBetweenTheRowsIsBetweenThem()
    {
        var heads = F(D(Figure(new() { ["age"] = 2f }), "build"), "heads");
        Assert.InRange(heads, 4.2f, 4.8f);
    }

    /// <summary>"The legs grow nearly twice as fast as the torso": the crotch sinks toward the middle.</summary>
    [Fact]
    public void TestTheLegsGrowIntoTheFigure()
    {
        var crotch = new[] { 1f, 3f, 5f, 10f, 15f, 18f }
            .Select(a => { var f = Figure(new() { ["age"] = a }); return Down(f, Y(f, "crotch")); }).ToList();
        for (var i = 1; i < crotch.Count; i++) Assert.True(crotch[i] < crotch[i - 1], $"age step {i}");
        Assert.Equal(0.71f, crotch[0], 2);
    }

    [Fact]
    public void TestABabysArmEndsAboveItsCrotch()
    {
        var baby = Figure(new() { ["age"] = 1f });
        Assert.True(Y(D(baby, "leftArm"), "wrist") < Y(baby, "crotch") - 0.05f * T);
    }

    [Fact]
    public void TestAChildIsNarrowerAndRounderHeadedForItsHead()
    {
        var adult = Figure();
        var child = Figure(new() { ["age"] = 3f });
        float Unit(Dictionary<string, object?> f, string key) => F(D(f, "build"), key) / F(f, "headUnit");
        Assert.True(Unit(child, "widthUnit") < Unit(adult, "widthUnit"));
        Assert.True(Unit(child, "legUnit") < Unit(adult, "legUnit"));
        float Round(Dictionary<string, object?> f) => F(D(f, "head"), "rx") / F(D(f, "head"), "ry");
        Assert.True(Round(child) > Round(adult));
    }

    [Fact]
    public void TestAGirlGrowsIntoTheFemaleBuild()
    {
        var girl = Figure(new() { ["build"] = "female", ["age"] = 16.5f });
        var crotch = Down(girl, Y(girl, "crotch"));
        Assert.InRange(crotch, 0.515f, 13f / 24f);
        Assert.Equal("female", D(girl, "build")["name"]);
    }

    [Fact]
    public void TestAChildCanBePosedAndSolved()
    {
        var pose = new Dictionary<string, object?>
        {
            ["lineOfAction"] = new Dictionary<string, object?> { ["shape"] = "C", ["turnDeg"] = 20f },
            ["rightArm"] = new Dictionary<string, object?> { ["shoulderDeg"] = -60f, ["elbowDeg"] = 20f }
        };
        var kid = Figure(new() { ["age"] = 5f, ["pose"] = pose });
        var geo = Toolkit.CreateFigureGeometry(kid);
        Assert.False(((CanvasPath)geo["silhouette"]!).IsEmpty);
    }

    /// <summary>
    /// Skia's union reported success and dropped the one-year-old's right shin at x = 1220 and not at
    /// x = 120: the thigh and shin meet on the same circle at the knee. A silhouette must not depend on
    /// where the figure stands.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(3f)]
    [InlineData(18f)]
    public void TestASilhouetteDoesNotDependOnWhereTheFigureStands(float age)
    {
        float Area(float x) => ((CanvasPath)Toolkit.CreateFigureGeometry(Toolkit.CreateMannequinFigure(x, 500f, 200f,
            new Dictionary<string, object?> { ["age"] = age, ["shoulderTiltDeg"] = 0f, ["pelvicTiltDeg"] = 0f }))["silhouette"]!).Area;
        var reference = Area(120f);
        foreach (var x in new[] { 1220f, 433.3f, 977.7f, 60.5f })
            Assert.Equal(reference, Area(x), reference * 0.005f);
    }

    /// <summary>
    /// A frame of the anim2 run: Skia's union reported failure building this posed figure, and ten
    /// scripts in a row died on it. A failed operation is retried before it is an error.
    /// </summary>
    [Fact]
    public void TestAFigureSkiaCouldNotUnionIsBuilt()
    {
        var pose = new Dictionary<string, object?>
        {
            ["lineOfAction"] = new Dictionary<string, object?> { ["shape"] = "C", ["turnDeg"] = 0f, ["leanDeg"] = 0f },
            ["neckDeg"] = 10.125000000000009,
            ["rightArm"] = new Dictionary<string, object?> { ["shoulderDeg"] = 107.168, ["elbowDeg"] = -10.740740740740735 },
            ["leftArm"] = new Dictionary<string, object?> { ["shoulderDeg"] = 75.25, ["elbowDeg"] = 15.370370370370367 }
        };
        float Area(float x) => ((CanvasPath)Toolkit.CreateFigureGeometry(Toolkit.CreateMannequinFigure(x, 58f, 300f,
            new Dictionary<string, object?> { ["pose"] = pose }))["silhouette"]!).Area;
        Assert.Equal(Area(140f), Area(340f), Area(140f) * 0.005f);
    }

    [Theory]
    [InlineData("build", "child")]
    [InlineData("age", -2f)]
    public void TestABadBuildOrAgeIsRefused(string key, object value) =>
        Assert.Throws<ArgumentException>(() => Figure(new() { [key] = value }));
    #endregion

    #region The head on a child
    [Fact]
    public void TestAChildsHeadHasAChildsFace()
    {
        var kid = Toolkit.CreateHeadForFigure(Figure(new() { ["age"] = 3f }));
        var adult = Toolkit.CreateHeadForFigure(Figure());
        float EyeDrop(Dictionary<string, object?> h) =>
            (F(h, "eyeLineY") - Y(h, "crown")) / F(D(h, "fit"), "headHeight");

        Assert.True(EyeDrop(kid) > EyeDrop(adult) + 0.05f, "a child's eyes sit lower in the head");
        Assert.Equal("loomis", D(kid, "fit")["skull"]);
        Assert.Equal("comic", D(adult, "fit")["skull"]);
        Assert.True((bool)Toolkit.VerifyHeadOrdering(kid)["ordered"]!);
    }

    [Fact]
    public void TestTheCallersCharacterWinsOverTheChildFace()
    {
        var fig = Figure(new() { ["age"] = 3f });
        var own = Toolkit.CreateHeadForFigure(fig, new Dictionary<string, object?>
        {
            ["character"] = new Dictionary<string, object?> { ["eyeLine"] = 0f }
        });
        var plain = Toolkit.CreateHeadForFigure(Figure(), new Dictionary<string, object?> { ["skull"] = "loomis" });
        float EyeDrop(Dictionary<string, object?> h) =>
            (F(h, "eyeLineY") - Y(h, "crown")) / F(D(h, "fit"), "headHeight");
        Assert.Equal(EyeDrop(plain), EyeDrop(own), 2);
    }
    #endregion
}
