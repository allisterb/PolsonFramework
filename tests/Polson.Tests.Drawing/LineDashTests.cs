namespace Polson.Tests.Drawing;

using System;
using System.Linq;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// <c>ctx.setLineDash([4, 4])</c> — standard HTML5 Canvas — threw "Property 'setLineDash' of object
/// is not a function", found by the Gemini logo run, which fell back to <c>Skia.PathEffect.dash</c>.
/// <para>
/// The state and the stroke paint already understood <c>LineDash</c> and composed a dash effect;
/// only the JS-facing entry points were missing. These check the pattern actually reaches the
/// pixels, not merely that the call exists.
/// </para>
/// </summary>
public class LineDashTests : TestsRuntime
{
    #region Methods
    /// <summary>Renders a horizontal line across the middle and counts runs of ink along it.</summary>
    private static int InkRuns(string dashSetup)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(200, 40);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 200, 40);
            x.strokeStyle = '#000000';
            x.lineWidth = 6;
            {{dashSetup}}
            x.beginPath();
            x.moveTo(0, 20);
            x.lineTo(200, 20);
            x.stroke();
            c;
            """, 200, 40, null, "png", 100);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);

        using var bitmap = SKBitmap.Decode(result.ImageBytes!);

        var runs = 0;
        var wasInk = false;
        for (var px = 0; px < bitmap.Width; px++)
        {
            var isInk = bitmap.GetPixel(px, 20).Red < 128;
            if (isInk && !wasInk) runs++;
            wasInk = isInk;
        }

        return runs;
    }
    #endregion

    #region Dash Rendering Tests
    /// <summary>A solid line is one unbroken run; the dashed one is many.</summary>
    [Fact]
    public void TestSetLineDashBreaksTheStroke()
    {
        var solid = InkRuns("");
        var dashed = InkRuns("x.setLineDash([8, 8]);");

        Assert.Equal(1, solid);
        Assert.True(dashed > 5, $"expected a broken line, got {dashed} run(s)");
    }

    /// <summary>An empty array clears the pattern, as in the DOM.</summary>
    [Fact]
    public void TestEmptyArrayClearsTheDash() =>
        Assert.Equal(1, InkRuns("x.setLineDash([8, 8]); x.setLineDash([]);"));

    /// <summary>A tighter pattern produces more dashes over the same span.</summary>
    [Fact]
    public void TestShorterPatternProducesMoreDashes() =>
        Assert.True(InkRuns("x.setLineDash([4, 4]);") > InkRuns("x.setLineDash([20, 20]);"));

    /// <summary>An odd-length pattern repeats to make it even: [5] means 5 on, 5 off.</summary>
    [Fact]
    public void TestOddLengthPatternRepeats()
    {
        var odd = InkRuns("x.setLineDash([6]);");
        var explicitPair = InkRuns("x.setLineDash([6, 6]);");

        Assert.Equal(explicitPair, odd);
    }
    #endregion

    #region Accessor Tests
    /// <summary>getLineDash reports the pattern back, and as a real JS array.</summary>
    [Fact]
    public void TestGetLineDashReturnsTheArray()
    {
        var result = new JsDrawingEngine().Execute("""
            const x = createCanvas(10, 10).getContext('2d');
            x.setLineDash([4, 8]);
            const d = x.getLineDash();
            log(`isArray=${Array.isArray(d)} value=${d.join(',')}`);
            exit('ok');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.Contains("isArray=true value=4,8", StringComparison.Ordinal));
    }

    /// <summary>Empty by default, so a script can read it before setting one.</summary>
    [Fact]
    public void TestGetLineDashIsEmptyByDefault()
    {
        var result = new JsDrawingEngine().Execute("""
            const x = createCanvas(10, 10).getContext('2d');
            log('len=' + x.getLineDash().length);
            exit('ok');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.Contains("len=0", StringComparison.Ordinal));
    }

    /// <summary>lineDashOffset shifts the pattern, which is what animates a marching-ants border.</summary>
    [Fact]
    public void TestLineDashOffsetShiftsThePattern()
    {
        var result = new JsDrawingEngine().Execute("""
            const x = createCanvas(10, 10).getContext('2d');
            x.lineDashOffset = 3.5;
            log('offset=' + x.lineDashOffset);
            exit('ok');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.Contains("offset=3.5", StringComparison.Ordinal));
    }

    /// <summary>The dash is part of saved state, so save/restore brackets it like any other style.</summary>
    [Fact]
    public void TestDashIsRestoredWithState()
    {
        var result = new JsDrawingEngine().Execute("""
            const x = createCanvas(10, 10).getContext('2d');
            x.setLineDash([4, 4]);
            x.save();
            x.setLineDash([20, 20]);
            x.restore();
            log('after=' + x.getLineDash().join(','));
            exit('ok');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.Contains("after=4,4", StringComparison.Ordinal));
    }
    #endregion
}
