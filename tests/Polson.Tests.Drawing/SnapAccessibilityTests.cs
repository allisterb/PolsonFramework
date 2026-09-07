namespace Polson.Tests.Drawing;

using System;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// <c>&lt;title&gt;</c> and <c>&lt;desc&gt;</c> — the accessible name and long description.
/// </summary>
/// <remarks>
/// <para>
/// <b>Neither could be emitted at all.</b> <c>el('title')</c> threw, and there was no other route, so
/// every SVG this studio produced was an unlabelled image however carefully it was drawn.
/// <c>polson://manual/27</c> §7 recorded it as a gap with no workaround.
/// </para>
/// <para>
/// Document order is the part worth pinning rather than the mere presence of the element: the
/// accessible name comes from the <b>first</b> <c>&lt;title&gt;</c> child, so one appended after the
/// artwork names nothing, and two titles on one element is an ambiguity rather than a fuller
/// description.
/// </para>
/// </remarks>
public class SnapAccessibilityTests : TestsRuntime
{
    #region Emission Tests
    [Fact]
    public void TestARootTitleAndDescriptionAreEmitted()
    {
        var result = Execute("""
            const p = Snap(300, 120);
            p.title('Apollo 11 descent stage fuel budget');
            p.desc('Mass distribution and the 752-second powered descent.');
            const xml = p.toString();
            log(String(xml.indexOf('<title>Apollo 11 descent stage fuel budget</title>') > 0)
              + ',' + String(xml.indexOf('<desc>Mass distribution') > 0));
            """);

        Assert.Equal("true,true", Logged(result));
    }

    /// <summary>Any element can carry its own title, not just the root.</summary>
    /// <remarks>
    /// A chart's bars are the case that matters: a reader who cannot see the plate still needs to know
    /// which bar is which, and that is per-element rather than per-document.
    /// </remarks>
    [Fact]
    public void TestAnyElementCanCarryATitle()
    {
        var result = Execute("""
            const p = Snap(300, 120);
            p.rect(20, 20, 100, 60).title('Descent propellant, 8,212 kg');
            log(String(p.toString().indexOf('8,212 kg') > 0));
            """);

        Assert.Equal("true", Logged(result));
    }

    [Fact]
    public void TestElCanNowCreateThem()
    {
        var result = Execute("""
            const p = Snap(200, 100);
            p.el('title').attr({ text: 'x' });
            p.el('desc');
            log(String(p.toString().indexOf('<title') > 0 && p.toString().indexOf('<desc') > 0));
            """);

        Assert.Equal("true", Logged(result));
    }
    #endregion

    #region Document Order Tests
    /// <summary>The title precedes the artwork, because the first one is the accessible name.</summary>
    [Fact]
    public void TestTheTitleComesFirst()
    {
        var result = Execute("""
            const p = Snap(300, 120);
            p.rect(20, 20, 100, 60).attr({ fill: '#1f6f8b' });
            p.title('Named after the artwork was drawn');
            const xml = p.toString();
            log(String(xml.indexOf('<title') < xml.indexOf('<rect')));
            """);

        Assert.Equal("true", Logged(result));
    }

    /// <summary>A description follows the title rather than displacing it.</summary>
    [Fact]
    public void TestTheDescriptionFollowsTheTitle()
    {
        var result = Execute("""
            const p = Snap(300, 120);
            p.desc('described first');
            p.title('titled second');
            const xml = p.toString();
            log(String(xml.indexOf('<title') < xml.indexOf('<desc')));
            """);

        Assert.Equal("true", Logged(result));
    }

    /// <summary>Setting a title twice replaces it, leaving exactly one.</summary>
    [Fact]
    public void TestSettingATitleTwiceReplacesIt()
    {
        var result = Execute("""
            const p = Snap(300, 120);
            p.title('first');
            p.title('second');
            const xml = p.toString();
            log((xml.split('<title').length - 1) + ',' + String(xml.indexOf('second') > 0 && xml.indexOf('first') < 0));
            """);

        Assert.Equal("1,true", Logged(result));
    }
    #endregion

    #region Methods
    private static DrawingExecutionResult Execute(string body)
    {
        var result = new JsDrawingEngine().Execute(body, 300, 120, null, "png", 100, render: false);
        Assert.True(result.Success, result.Error);
        return result;
    }

    private static string Logged(DrawingExecutionResult result) =>
        string.Join(" ", result.Logs).Replace("[LOG]", string.Empty, StringComparison.Ordinal).Trim();
    #endregion
}
