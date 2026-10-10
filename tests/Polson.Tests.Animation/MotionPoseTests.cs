namespace Polson.Tests.Animation;

using System;
using System.Collections.Generic;
using System.Linq;

using Polson.Animation;

using Xunit;

/// <summary>
/// Pose blending: Synfig's weighted average as a node, and the face's pose library, where a stored set of channel values
/// is blended in by a weight, as blend shapes are.
/// </summary>
public class MotionPoseTests : TestsRuntime
{
    [Fact]
    public void TheWeightedAverageIsSynfigs()
    {
        var n = new MotionToolkit().Nodes;
        var even = n.WeightedAverage("real", new object[] { new object[] { 0d, 1d }, new object[] { 10d, 3d } });
        Assert.Equal(7.5, (double)even.At(0), 9);

        // Weights summing to zero fall back to the plain average, as Synfig's average_generic does.
        var zero = n.WeightedAverage("real", new object[] { new object[] { 0d, 0d }, new object[] { 10d, 0d } });
        Assert.Equal(5d, (double)zero.At(0), 9);

        var keyed = n.WeightedAverage("vector", new object[]
        {
            new Dictionary<string, object?> { ["value"] = new[] { 0d, 0d }, ["weight"] = n.Linear("real", -1, 1) },
            new Dictionary<string, object?> { ["value"] = new[] { 100d, 50d }, ["weight"] = n.Linear("real", 1, 0) }
        });
        var half = (IDictionary<string, object?>)keyed.At(0.5);
        Assert.Equal(50d, (double)half["x"]!, 9);
        Assert.Equal(25d, (double)half["y"]!, 9);

        Assert.Throws<ArgumentException>(() => n.WeightedAverage("real", new object[] { new object[] { 1d } }));
        Assert.Throws<ArgumentException>(() => n.WeightedAverage("real", new object[] { new object[] { "#ff0000", 1d } }));
    }

    static readonly Dictionary<string, object?> Library = new()
    {
        ["smile"] = Opts(("AU12", 1d), ("AU6", 0.8d), ("lowerLid", 2d)),
        ["surprised"] = Opts(("browTop", 1d), ("upperLid", 1.5d), ("AU26", 0.4d))
    };

    static MotionFace Face(params (string, object?)[] channels) =>
        new MotionToolkit().Composition(Opts(("width", 400d), ("height", 400d), ("duration", 1d)))
            .Face(Opts(("origin", new[] { 200d, 200d }), ("height", 300d), ("poses", Library), ("channels", Opts(channels))));

    /// <summary>A pose at weight 1 is its channels set directly; at a half it is half of each, measured from the normal.</summary>
    [Fact]
    public void APoseIsItsChannelsByItsWeight()
    {
        Assert.Equal(Landmarks(Face(("AU12", 1d), ("AU6", 0.8d), ("lowerLid", 2d)).HeadAt(0)), Landmarks(Face(("smile", 1d)).HeadAt(0)));
        Assert.Equal(Landmarks(Face(("AU12", 0.5d), ("AU6", 0.4d), ("lowerLid", 2.5d)).HeadAt(0)), Landmarks(Face(("smile", 0.5d)).HeadAt(0)));
        Assert.Equal(Landmarks(Face(("AU12", 0d)).HeadAt(0)), Landmarks(Face(("smile", 0d)).HeadAt(0)));
    }

    /// <summary>Poses add, as blend shapes do, and move a setting given directly by their distance from the normal.</summary>
    [Fact]
    public void PosesAddToEachOtherAndToTheChannels()
    {
        var both = Face(("smile", 1d), ("surprised", 1d)).HeadAt(0);
        var byHand = Face(("AU12", 1d), ("AU6", 0.8d), ("AU26", 0.4d), ("lowerLid", 2d), ("browTop", 1d), ("upperLid", 1.5d)).HeadAt(0);
        Assert.Equal(Landmarks(byHand), Landmarks(both));

        // upperLid 4 set directly, and half a pose holding it at 1.5: 4 + 0.5 × (1.5 − 3) = 3.25.
        Assert.Equal(Landmarks(Face(("upperLid", 3.25d), ("browTop", 2d), ("AU26", 0.2d)).HeadAt(0)),
                     Landmarks(Face(("upperLid", 4d), ("surprised", 0.5d)).HeadAt(0)));
    }

    [Fact]
    public void APoseCanBeSetOnOneSide()
    {
        var rest = Face().HeadAt(0);
        var half = Face(("near.smile", 1d)).HeadAt(0);
        Assert.NotEqual(Point(rest, "mouthGuides", "rightCorner"), Point(half, "mouthGuides", "rightCorner"));
        Assert.Equal(Point(rest, "mouthGuides", "leftCorner"), Point(half, "mouthGuides", "leftCorner"));
    }

    [Fact]
    public void AKeyedWeightBlendsThePoseOverTime()
    {
        var n = new MotionToolkit().Nodes;
        var face = Face(("smile", n.Linear("real", 1, 0)));
        Assert.Equal(new[] { "smile", "surprised" }, face.Poses.ToArray());
        Assert.Equal(Landmarks(Face(("smile", 0.25d)).HeadAt(0)), Landmarks(face.HeadAt(0.25)));
    }

    [Fact]
    public void ABadPoseIsRefusedByName()
    {
        var comp = new MotionToolkit().Composition();
        Assert.Contains("'joy'", Assert.Throws<ArgumentException>(() =>
            comp.Face(Opts(("poses", Opts(("joy", Opts(("AU12", 1d)))))))).Message);
        Assert.Throws<ArgumentException>(() => comp.Face(Opts(("poses", Opts(("grin", Opts(("lowerlid", 2d))))))));
        var node = new MotionToolkit().Nodes.Linear("real", 1, 0);
        Assert.Throws<ArgumentException>(() => comp.Face(Opts(("poses", Opts(("grin", Opts(("AU12", node))))))));
    }

    #region Helpers
    private static string Landmarks(Dictionary<string, object?> head) =>
        string.Join(";", new[] { ("mouthGuides", "rightCorner"), ("mouthGuides", "leftCorner"), ("nearBrow", "peak"), ("farBrow", "inner"), ("jaw", "chin") }
            .Select(k => Point(head, k.Item1, k.Item2)).Select(p => $"{p.X:0.###},{p.Y:0.###}"))
        + $";{Value(head, "nearEye", "height"):0.###};{Value(head, "nearEye", "lowerLift"):0.###};{Value(head, "farEye", "foldLift"):0.###}";

    private static (float X, float Y) Point(Dictionary<string, object?> head, string group, string key)
    {
        var p = (IDictionary<string, object?>)((IDictionary<string, object?>)head[group]!)[key]!;
        return (Convert.ToSingle(p["x"]), Convert.ToSingle(p["y"]));
    }

    private static float Value(Dictionary<string, object?> head, string group, string key) =>
        Convert.ToSingle(((IDictionary<string, object?>)head[group]!)[key]);

    private static Dictionary<string, object?> Opts(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);
    #endregion
}
