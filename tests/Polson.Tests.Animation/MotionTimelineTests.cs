namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;

using Polson.Drawing.Skia;
using Polson.Drawing.Svg;

using Xunit;

/// <summary>
/// The score, tested with no JavaScript engine at all.
/// </summary>
/// <remarks>
/// Testability is the stated reason the timeline is C# rather than the twenty lines of JavaScript it
/// replaces, so these pass plain delegates as setters and never start an engine. What they cannot
/// cover is the marshalling boundary — that belongs with the engine tests.
/// </remarks>
public class MotionTimelineTests : TestsRuntime
{
    #region Methods (private)
    static MotionTimeline NewTimeline() => new MotionToolkit().Timeline();

    /// <summary>A timeline holding one recorded tween, and the list it records into.</summary>
    static (MotionTimeline Timeline, List<double> Seen) Recording(double at = 0d, double dur = 100d)
    {
        var seen = new List<double>();
        var tl = NewTimeline();
        tl.Tween(0d, 100d, v => seen.Add(v), new MotionEntryOptions { At = at, Dur = dur });
        return (tl, seen);
    }

    static double Last(List<double> seen) => seen[^1];
    #endregion

    #region Tests — position grammar
    [Fact]
    public void TestAnAbsolutePositionIsTakenAsGiven()
    {
        var tl = NewTimeline();
        tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { At = 1200d, Dur = 300d });
        Assert.Equal(1500d, tl.Duration);
    }

    /// <summary>Omitted means "at the end" — sequential append, which is the common case.</summary>
    [Fact]
    public void TestAnOmittedPositionAppends()
    {
        var tl = NewTimeline();
        tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { Dur = 200d });
        tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { Dur = 300d });
        Assert.Equal(500d, tl.Duration);
    }

    /// <summary>
    /// Where a position actually put the entry, read back by seeking.
    /// </summary>
    /// <remarks>
    /// <see cref="MotionTimeline.Duration"/> cannot answer this — it is the max over every entry, so
    /// an entry placed <i>earlier</i> than an existing one leaves it unchanged. A zero-length step
    /// flips from 0 to 1 exactly at its own start, which pins the placement to the millisecond.
    /// </remarks>
    static double StartOf(MotionTimeline tl, object at)
    {
        var flipped = -1d;
        tl.Tween(0d, 1d, v => flipped = v, new MotionEntryOptions { At = at, Dur = 0d });

        // Binary search for the flip, over a range wide enough for every case here.
        double low = -10_000d, high = 10_000d;
        for (var i = 0; i < 60; i++)
        {
            var mid = (low + high) / 2d;
            tl.Seek(mid);
            if (flipped >= 1d) high = mid; else low = mid;
        }

        return Math.Round(high, 6);
    }

    [Theory]
    [InlineData("+=200", 400d)]
    [InlineData("-=200", 0d)]
    public void TestARelativePositionIsMeasuredFromTheEnd(string at, double expected)
    {
        var tl = NewTimeline();
        tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { At = 0d, Dur = 200d });
        Assert.Equal(expected, StartOf(tl, at), 3);
    }

    [Theory]
    [InlineData("<", 500d)]
    [InlineData(">", 700d)]
    [InlineData("<+=100", 600d)]
    [InlineData(">-=50", 650d)]
    public void TestAPositionRelativeToThePreviousEntry(string at, double expectedStart)
    {
        var tl = NewTimeline();
        tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { At = 500d, Dur = 200d });
        Assert.Equal(expectedStart, StartOf(tl, at), 3);
    }

    /// <summary>With nothing to be relative to, these are 0 rather than an error.</summary>
    [Theory]
    [InlineData("<")]
    [InlineData(">")]
    public void TestThePreviousAnchorsResolveToZeroOnAnEmptyTimeline(string at) =>
        Assert.Equal(0d, StartOf(NewTimeline(), at), 3);

    [Fact]
    public void TestALabelPlacesAnEntry()
    {
        var tl = NewTimeline();
        tl.Label("chartIn", 800d);
        tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { At = "chartIn", Dur = 100d });
        Assert.Equal(900d, tl.Duration);
    }

    [Fact]
    public void TestALabelCanBeOffset()
    {
        var tl = NewTimeline();
        tl.Label("chartIn", 800d);
        tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { At = "chartIn+=200", Dur = 100d });
        Assert.Equal(1100d, tl.Duration);
    }

    [Fact]
    public void TestALabelWithNoPositionTakesTheCurrentEnd()
    {
        var tl = NewTimeline();
        tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { At = 0d, Dur = 640d });
        tl.Label("afterIntro");
        Assert.Equal(640d, (double)tl.Labels["afterIntro"]!);
    }

    /// <summary>
    /// A mistyped label throws and names itself.
    /// </summary>
    /// <remarks>
    /// Resolving it to 0 would place the beat at the start of the film and animate happily — wrong,
    /// silent and plausible, which is the failure class this project treats as worst.
    /// </remarks>
    [Fact]
    public void TestAnUnknownLabelThrowsAndNamesIt()
    {
        var tl = NewTimeline();
        tl.Label("chartIn", 800d);

        var ex = Assert.Throws<ArgumentException>(() =>
            tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { At = "chartln" }));

        Assert.Contains("chartln", ex.Message);
        Assert.Contains("chartIn", ex.Message);      // and says what it could have meant
        Assert.Equal(0, tl.Count);
    }

    [Fact]
    public void TestAMalformedOffsetThrows() =>
        Assert.Throws<ArgumentException>(() =>
            NewTimeline().Tween(0d, 1d, _ => { }, new MotionEntryOptions { At = "+=" }));
    #endregion

    #region Tests — purity and seek safety
    /// <summary>Before an entry starts, its `from` is what is applied — not whatever was there.</summary>
    [Fact]
    public void TestAnEntryAppliesItsFromValueBeforeItStarts()
    {
        var (tl, seen) = Recording(at: 500d, dur: 100d);
        tl.Seek(0d);
        Assert.Equal(0d, Last(seen));
    }

    [Fact]
    public void TestAnEntryHoldsItsToValueAfterItEnds()
    {
        var (tl, seen) = Recording(at: 500d, dur: 100d);
        tl.Seek(5000d);
        Assert.Equal(100d, Last(seen));
    }

    /// <summary>The property the whole design rests on.</summary>
    [Fact]
    public void TestSeekingBackwardsReproducesTheSameValues()
    {
        var (tl, seen) = Recording(at: 0d, dur: 1000d);

        var ascending = new List<double>();
        for (var t = 0d; t <= 1000d; t += 50d) { tl.Seek(t); ascending.Add(Last(seen)); }

        var descending = new List<double>();
        for (var t = 1000d; t >= 0d; t -= 50d) { tl.Seek(t); descending.Add(Last(seen)); }
        descending.Reverse();

        Assert.Equal(ascending, descending);
    }

    [Fact]
    public void TestReSeekingAVisitedTimeReproducesItExactly()
    {
        var (tl, seen) = Recording(at: 0d, dur: 1000d);

        tl.Seek(377d);
        var first = Last(seen);
        tl.Seek(0d);
        tl.Seek(999d);
        tl.Seek(377d);

        Assert.Equal(first, Last(seen));
    }

    [Fact]
    public void TestAnEmptyTimelineHasNoDurationAndSeeksHarmlessly()
    {
        var tl = NewTimeline();
        Assert.Equal(0d, tl.Duration);
        Assert.Equal(0, tl.Count);
        Assert.Same(tl, tl.Seek(1234d));
    }

    [Fact]
    public void TestASetterIsRequired() =>
        Assert.Throws<ArgumentNullException>(() => NewTimeline().Tween(0d, 1d, null));
    #endregion

    #region Tests — composition
    /// <summary>Duration is the furthest end, not the sum — overlapping entries do not extend it.</summary>
    [Fact]
    public void TestDurationIsTheMaximumEndNotTheSum()
    {
        var tl = NewTimeline();
        tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { At = 0d, Dur = 1000d });
        tl.Tween(0d, 1d, _ => { }, new MotionEntryOptions { At = 200d, Dur = 300d });
        Assert.Equal(1000d, tl.Duration);
    }

    /// <summary>Two entries on one property: the later one wins, because they apply in order.</summary>
    [Fact]
    public void TestTheLaterEntryWinsOnTheSameProperty()
    {
        var value = -1d;
        var tl = NewTimeline();
        tl.Tween(0d, 10d, v => value = v, new MotionEntryOptions { At = 0d, Dur = 100d });
        tl.Tween(50d, 60d, v => value = v, new MotionEntryOptions { At = 0d, Dur = 100d });

        tl.Seek(100d);
        Assert.Equal(60d, value);
    }

    [Fact]
    public void TestAnEasingIsAppliedToTheStatus()
    {
        var seen = new List<double>();
        var tl = NewTimeline();
        tl.Tween(0d, 100d, seen.Add, new MotionEntryOptions
        {
            At = 0d,
            Dur = 100d,
            Easing = n => n * n
        });

        tl.Seek(50d);
        Assert.Equal(25d, Last(seen), 6);
    }

    [Fact]
    public void TestATimelineDefaultEasingApplies()
    {
        var seen = new List<double>();
        var tl = new MotionToolkit().Timeline(new MotionTimelineOptions
        {
            Defaults = new MotionTimelineDefaults { Dur = 200d, Easing = n => n * n }
        });

        tl.Tween(0d, 100d, seen.Add);
        Assert.Equal(200d, tl.Duration);

        tl.Seek(100d);
        Assert.Equal(25d, Last(seen), 6);
    }
    #endregion

    #region Tests — attributes
    static SnapPaper NewPaper() => Snap.Create(400, 300);

    [Fact]
    public void TestANumericAttributeLerpsAndLandsExactlyOnTheTarget()
    {
        var paper = NewPaper();
        var circle = paper.Circle(50, 50, 10);

        var tl = NewTimeline();
        tl.To(circle, new Dictionary<string, object?> { ["cx"] = 250d },
            new MotionEntryOptions { At = 0d, Dur = 100d });

        tl.Seek(50d);
        Assert.Equal(150d, Convert.ToDouble(circle.Attr("cx")), 3);

        tl.Seek(100d);
        Assert.Equal(250d, Convert.ToDouble(circle.Attr("cx")), 3);
    }

    [Fact]
    public void TestAColourAttributeLerpsChannelWise()
    {
        var paper = NewPaper();
        var circle = paper.Circle(50, 50, 10);
        circle.Attr("fill", "#000000");

        var tl = NewTimeline();
        tl.To(circle, new Dictionary<string, object?> { ["fill"] = "#ffffff" },
            new MotionEntryOptions { At = 0d, Dur = 100d });

        tl.Seek(50d);
        var midpoint = circle.Attr("fill")?.ToString();
        Assert.Equal("#808080", midpoint, ignoreCase: true);
    }

    /// <summary>The base is read when the entry is added, so a later mutation cannot change history.</summary>
    [Fact]
    public void TestTheBaseValueIsCapturedWhenTheEntryIsAdded()
    {
        var paper = NewPaper();
        var circle = paper.Circle(50, 50, 10);

        var tl = NewTimeline();
        tl.To(circle, new Dictionary<string, object?> { ["cx"] = 100d },
            new MotionEntryOptions { At = 0d, Dur = 100d });

        circle.Attr("cx", 999d);        // moved behind the timeline's back
        tl.Seek(0d);

        Assert.Equal(50d, Convert.ToDouble(circle.Attr("cx")), 3);
    }

    [Fact]
    public void TestAnUninterpolatableAttributeThrowsNamingIt()
    {
        var paper = NewPaper();
        var circle = paper.Circle(50, 50, 10);

        var ex = Assert.Throws<ArgumentException>(() =>
            NewTimeline().To(circle, new Dictionary<string, object?> { ["font-family"] = "Georgia" }));

        Assert.Contains("font-family", ex.Message);
        Assert.Contains("set", ex.Message);
    }

    [Fact]
    public void TestToNeedsAttributes()
    {
        var circle = NewPaper().Circle(50, 50, 10);
        Assert.Throws<ArgumentException>(() => NewTimeline().To(circle, new Dictionary<string, object?>()));
    }

    /// <summary>`set` is a step: the base before, the value at and after.</summary>
    [Fact]
    public void TestSetIsAStepFunction()
    {
        var paper = NewPaper();
        var circle = paper.Circle(50, 50, 10);
        circle.Attr("fill", "#112233");

        var tl = NewTimeline();
        tl.Set(circle, new Dictionary<string, object?> { ["fill"] = "#ff0000" },
            new MotionEntryOptions { At = 500d });

        tl.Seek(499d);
        Assert.Equal("#112233", circle.Attr("fill")?.ToString(), ignoreCase: true);

        tl.Seek(500d);
        Assert.Equal("#ff0000", circle.Attr("fill")?.ToString(), ignoreCase: true);

        tl.Seek(0d);                    // and it reverts on a backwards seek
        Assert.Equal("#112233", circle.Attr("fill")?.ToString(), ignoreCase: true);
    }

    /// <summary>`set` accepts what could never be tweened — that is its job.</summary>
    [Fact]
    public void TestSetTakesAValueNoTweenCouldInterpolate()
    {
        var text = NewPaper().Text(10, 20, "hello");
        var tl = NewTimeline();
        tl.Set(text, new Dictionary<string, object?> { ["font-family"] = "Georgia" },
            new MotionEntryOptions { At = 100d });

        tl.Seek(100d);
        Assert.Equal("Georgia", text.Attr("font-family")?.ToString());
    }
    #endregion

    #region Tests — show
    [Fact]
    public void TestShowHidesOutsideItsWindowInBothDirections()
    {
        var paper = NewPaper();
        var circle = paper.Circle(50, 50, 10);

        var tl = NewTimeline();
        tl.Show(circle, new MotionEntryOptions { From = 200d, To = 600d });

        tl.Seek(100d);
        Assert.Equal("none", circle.Attr("display")?.ToString());

        tl.Seek(400d);
        Assert.NotEqual("none", circle.Attr("display")?.ToString());

        tl.Seek(800d);
        Assert.Equal("none", circle.Attr("display")?.ToString());

        tl.Seek(400d);                  // back inside the window
        Assert.NotEqual("none", circle.Attr("display")?.ToString());
    }

    [Fact]
    public void TestShowWithNoEndStaysVisible()
    {
        var circle = NewPaper().Circle(50, 50, 10);
        var tl = NewTimeline();
        tl.Show(circle, new MotionEntryOptions { From = 100d });

        tl.Seek(50d);
        Assert.Equal("none", circle.Attr("display")?.ToString());

        tl.Seek(1_000_000d);
        Assert.NotEqual("none", circle.Attr("display")?.ToString());
    }

    [Fact]
    public void TestShowRefusesAWindowThatEndsBeforeItStarts()
    {
        var circle = NewPaper().Circle(50, 50, 10);
        Assert.Throws<ArgumentException>(() =>
            NewTimeline().Show(circle, new MotionEntryOptions { From = 600d, To = 200d }));
    }
    #endregion

    #region Tests — stagger
    [Fact]
    public void TestStaggerPlacesOneEntryPerTargetAndExtendsTheDuration()
    {
        var paper = NewPaper();
        var circles = Enumerable.Range(0, 4).Select(i => paper.Circle(10 + i * 20, 50, 5)).ToArray();

        var tl = NewTimeline();
        tl.Stagger(circles, new Dictionary<string, object?> { ["r"] = 20d },
            new MotionEntryOptions { At = 0d, Dur = 100d, Each = 50d });

        Assert.Equal(4, tl.Count);
        Assert.Equal(250d, tl.Duration);        // last starts at 150, runs 100
    }

    [Fact]
    public void TestStaggeredTargetsReachTheirValueAtDifferentTimes()
    {
        var paper = NewPaper();
        var first = paper.Circle(10, 50, 5);
        var second = paper.Circle(30, 50, 5);

        var tl = NewTimeline();
        tl.Stagger(new[] { first, second }, new Dictionary<string, object?> { ["r"] = 25d },
            new MotionEntryOptions { At = 0d, Dur = 100d, Each = 100d });

        tl.Seek(100d);
        Assert.Equal(25d, Convert.ToDouble(first.Attr("r")), 3);        // finished
        Assert.Equal(5d, Convert.ToDouble(second.Attr("r")), 3);        // not started
    }

    [Fact]
    public void TestStaggerRefusesSomethingThatIsNotAnElement() =>
        Assert.Throws<ArgumentException>(() =>
            NewTimeline().Stagger(new object[] { "a circle" }, new Dictionary<string, object?> { ["r"] = 1d }));
    #endregion
}
