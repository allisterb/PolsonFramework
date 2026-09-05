namespace Polson.Tests.MCPServer;

using System;
using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// An easing reaches its caller from every position a script can put it in.
/// </summary>
/// <remarks>
/// <para>
/// The one that failed is <b>called through an object</b> — <c>{ ease: mina.elastic }</c> followed by
/// <c>t.ease(0.5)</c> — which is the natural spelling for a timeline entry and the shape
/// <c>docs/motion-score-api.md</c> is built on. It threw
/// <c>Object type Polson.Drawing.Svg.Mina does not match target type System.Dynamic.ExpandoObject</c>,
/// a message naming neither easings nor the line responsible, and the published workaround in
/// <c>docs/Polson.core.md</c> was to wrap every easing in a closure.
/// </para>
/// <para>
/// The fix is in <see cref="Polson.Drawing.Svg.Mina"/>: the easings are delegate-valued properties,
/// so a delegate carries its own target and <c>this</c> never enters into it. These tests exist
/// because the shape that fixes it looks like the shape worth tidying away — a method is the obvious
/// form for <c>linear(n)</c> — and the regression would surface far from the change.
/// </para>
/// </remarks>
public class MinaInteropTests : TestsRuntime
{
    #region Methods (private)
    static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, render: false);

    static string Logged(string script)
    {
        var result = Run(script);
        Assert.True(result.Success, result.Error);
        return string.Join(" ", result.Logs);
    }
    #endregion

    #region Tests
    /// <summary>The four positions, one test each, so a failure says which one broke.</summary>
    [Fact]
    public void TestAnEasingCalledDirectly() =>
        Assert.Contains("1.015625", Logged("log(String(mina.elastic(0.5)));"));

    [Fact]
    public void TestAnEasingCalledThroughALocal() =>
        Assert.Contains("1.015625", Logged("const f = mina.elastic; log(String(f(0.5)));"));

    /// <summary>This is the one that threw.</summary>
    [Fact]
    public void TestAnEasingCalledThroughAnObjectProperty() =>
        Assert.Contains("1.015625", Logged("const t = { ease: mina.elastic }; log(String(t.ease(0.5)));"));

    [Fact]
    public void TestAnEasingCalledThroughAnArrayElement() =>
        Assert.Contains("1.015625", Logged("const a = [mina.elastic]; log(String(a[0](0.5)));"));

    /// <summary>An easing handed to a JS function it did not come from — a score applying one per entry.</summary>
    [Fact]
    public void TestAnEasingPassedIntoAJsFunction() =>
        Assert.Contains("1.015625", Logged(
            "const apply = (e, n) => e(n); log(String(apply(mina.elastic, 0.5)));"));

    /// <summary>Every published easing, in the position that used to fail.</summary>
    [Theory]
    [InlineData("linear")]
    [InlineData("easein")]
    [InlineData("easeout")]
    [InlineData("easeinout")]
    [InlineData("backin")]
    [InlineData("backout")]
    [InlineData("bounce")]
    [InlineData("elastic")]
    public void TestEveryEasingSurvivesAnObjectProperty(string easing)
    {
        var logs = Logged($"const t = {{ ease: mina.{easing} }}; log(String(t.ease(0.5)));");
        Assert.False(logs.Contains("NaN") || logs.Contains("undefined"),
            $"mina.{easing} through an object property produced: {logs}");
    }

    [Fact]
    public void TestTimeIsCallableAndReturnsMilliseconds() =>
        Assert.Contains("true", Logged("log(String(mina.time() > 1600000000000));"));

    /// <summary>
    /// The easings answer the same numbers as before they became properties.
    /// </summary>
    /// <remarks>
    /// Expected values are Snap.svg's own definitions evaluated at n = 0.5, not a recording of what
    /// this implementation happened to print — a regression test against its own output would pass
    /// whatever the code did. The widened tolerance on <c>elastic</c> is the float-to-double change.
    /// </remarks>
    [Theory]
    [InlineData("linear", 0.5)]
    [InlineData("easeout", 0.7071067811865476)]     // sin(pi/4)
    [InlineData("easein", 0.2928932188134524)]      // 1 - cos(pi/4)
    [InlineData("easeinout", 0.5)]                  // 0.5 * (1 - cos(pi/2))
    [InlineData("backin", -0.0876975)]              // 0.25 * (2.70158 * 0.5 - 1.70158)
    [InlineData("backout", 1.0876975)]
    public void TestEasingValuesAreUnchanged(string easing, double expected)
    {
        var logs = Logged($"log(String(mina.{easing}(0.5)));");
        var printed = logs.Split(' ').LastOrDefault() ?? string.Empty;
        Assert.True(double.TryParse(printed, out var actual), $"could not read a number from: {logs}");
        Assert.Equal(expected, actual, 6);
    }

    /// <summary>
    /// A member that is not there is still known to be absent.
    /// </summary>
    /// <remarks>
    /// This is what the rejected JS-prelude fix would have cost. Rebinding <c>mina</c> to plain
    /// closures makes it an <c>ExpandoObject</c>, and <c>MemberIndex</c> answers <c>true</c> for any
    /// name on one — so <c>has</c> stops discriminating and <c>suggest</c> starts reporting that a
    /// misspelling "exists on ExpandoObject", naming a type no script author has heard of.
    /// </remarks>
    [Fact]
    public void TestMinaKeepsItsStrictMemberSurface()
    {
        Assert.Contains("true", Logged("log(String(has(mina, 'elastic')));"));
        Assert.Contains("false", Logged("log(String(has(mina, 'nonsense')));"));

        var advice = Logged("log(suggest(mina, 'nonsense'));");
        Assert.Contains("'Mina' has no property or method 'nonsense'", advice);
        Assert.DoesNotContain("ExpandoObject", advice);
    }
    #endregion
}
