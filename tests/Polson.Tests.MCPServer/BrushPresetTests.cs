namespace Polson.Tests.MCPServer;

using Polson.MCPServer;
using Xunit;

/// <summary>
/// A brush preset must obey being modified, because the SDK reference invites exactly that: "take one
/// and change a single part rather than starting over".
/// </summary>
/// <remarks>
/// The properties were get-only, and the resulting failure was the worst shape available. Assigning
/// <c>brush.color</c> from a script <b>read back as the new value</b> — Jint shadowed the CLR property
/// with a plain JS one — while <c>UseBrush</c> went on reading the CLR property and drew the original
/// colour. A script could therefore set a value, verify it, and still be wrong. <c>brush.lineWidth</c>
/// failed differently again: it did not even read back.
/// <para>
/// These tests run through the JS engine rather than against the C# type, because that interop layer
/// is where the defect lived; asserting on a C# auto-property would prove nothing about it.
/// </para>
/// </remarks>
public class BrushPresetTests : TestsRuntime
{
    #region Methods
    /// <summary>The mark obeys the modified colour, not just the read-back.</summary>
    [Fact]
    public void TestAModifiedColourReachesTheMark()
    {
        var result = Run("""
            const b = Skia.Brush.ink('#0a0a0c', 6);
            b.color = '#ff0000';
            const c = createCanvas(120, 60);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 120, 60);
            x.useBrush(b);
            x.beginPath(); x.moveTo(10, 30); x.lineTo(110, 30); x.stroke();
            log('read=' + b.color + ' drawn=' + c.bitmap.palette(2)[1].color);
            c;
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("read=#ff0000 drawn=#FF0000", result.Logs[0]);
    }

    /// <summary>Width was the part that did not even read back.</summary>
    [Fact]
    public void TestAModifiedWidthReachesTheContext()
    {
        var result = Run("""
            const b = Skia.Brush.pencil();
            b.lineWidth = 12;
            const ctx = createCanvas(40, 40).getContext('2d');
            ctx.useBrush(b);
            log('read=' + b.lineWidth + ' applied=' + ctx.lineWidth);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("read=12 applied=12", result.Logs[0]);
    }

    /// <summary>
    /// A preset's parts can be swapped, which is how one medium becomes a variant of another.
    /// </summary>
    [Fact]
    public void TestPartsCanBeSwappedAndCleared()
    {
        var result = Run("""
            const b = Skia.Brush.pencil();
            b.texture = null;                                  // let the path run clean
            b.edge = Skia.MaskFilter.blur(3, 'normal');        // and soften it differently
            log('grain=' + (b.grain ? 'y' : 'n') + ' texture=' + (b.texture ? 'y' : 'n') +
                ' edge=' + (b.edge ? 'y' : 'n'));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("grain=y texture=n edge=y", result.Logs[0]);
    }

    /// <summary>
    /// <c>useBrush</c> clears what a preset leaves null rather than inheriting the previous medium's.
    /// Without this, every medium after the first carries the one before it.
    /// </summary>
    [Fact]
    public void TestSwitchingMediumClearsWhatTheNewOneDoesNotSet()
    {
        var result = Run("""
            const ctx = createCanvas(40, 40).getContext('2d');
            ctx.useBrush(Skia.Brush.pencil());      // has a soft edge
            const withPencil = ctx.maskFilter ? 'set' : 'null';
            ctx.useBrush(Skia.Brush.ink());         // has none
            log('pencil=' + withPencil + ' ink=' + (ctx.maskFilter ? 'set' : 'null'));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("pencil=set ink=null", result.Logs[0]);
    }

    /// <summary>
    /// A seeded mark is reproducible, which is what lets a stage be re-run without the drawing
    /// shifting underneath it.
    /// </summary>
    [Fact]
    public void TestASeededMarkIsReproducible()
    {
        var result = Run("""
            function stroke(seed) {
                const c = createCanvas(160, 50);
                const x = c.getContext('2d');
                x.fillStyle = '#ffffff';
                x.fillRect(0, 0, 160, 50);
                x.useBrush(Skia.Brush.pencil('#3a3a3a', 6, 1, seed));
                x.beginPath(); x.moveTo(15, 25); x.lineTo(145, 25); x.stroke();
                return c.toBitmap();
            }
            log('same=' + stroke(42).diff(stroke(42)).identical +
                ' different=' + stroke(42).diff(stroke(99)).identical);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("same=true different=false", result.Logs[0]);
    }
    #endregion

    #region Methods (private)
    private static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, 160, 60, null, "png", 90);
    #endregion
}
