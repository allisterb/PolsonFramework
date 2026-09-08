namespace Polson.Tests.MCPServer;

using Polson.MCPServer;
using Xunit;

/// <summary>
/// What <c>JSON.stringify</c> does to an SDK result, and that the hook controlling it is reachable.
/// </summary>
/// <remarks>
/// Two faults met here. <c>JSON.stringify</c> walked the CLR object, so it transcribed raw byte
/// buffers as decimal arrays and named every field in PascalCase — and the engine's own
/// <c>InteropProtocolMembers</c> exemption answered <c>toJSON</c> as <c>undefined</c>
/// <b>unconditionally</b>, so a <c>ToJSON()</c> written to fix the first fault could never be
/// reached. The exemption exists so an <i>absent</i> hook reads as absent rather than throwing;
/// making it also shadow a present one is what this pins.
/// <para>
/// <b>The cost was measured on a live deployed run and it is not a one-off.</b> A stringified asset
/// took one turn's input from ~32k tokens to ~555k, and because it changed the prompt prefix,
/// <c>cached</c> fell from 27,590 to zero and stayed there — every later turn re-paid the full
/// amount, and four of them tripped a 2,000,000-token breaker.
/// </para>
/// <para>
/// <c>ImageData</c> is the subject because it is the worst case and needs no network: its buffer is
/// <c>width × height × 4</c> uncompressed, so a 1600 × 1200 canvas is 7.68 MB, which stringified as
/// numbers is roughly 30 MB of text.
/// </para>
/// </remarks>
public class SerializationHookTests : TestsRuntime
{
    #region Methods
    private static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, 32, 32, null, "png", 100, render: false);

    /// <summary>The single logged line, which is what each script here reports its answer through.</summary>
    private static string Logged(string script)
    {
        var result = Run(script);
        Assert.True(result.Success, result.Error);
        var line = Assert.Single(result.Logs, l => l.StartsWith("[LOG] "));
        return line["[LOG] ".Length..];
    }
    #endregion

    #region Tests
    /// <summary>The regression: a real <c>toJSON</c> must be reachable, not shadowed by the exemption.</summary>
    [Fact]
    public void TestATypeThatImplementsTheHookGetsToUseIt()
    {
        var text = Logged("""
            const img = createCanvas(8, 8).getContext('2d').getImageData(0, 0, 8, 8);
            log(typeof img.toJSON);
            """);

        Assert.Equal("function", text);
    }

    /// <summary>The buffer is reported as a length, never transcribed.</summary>
    [Fact]
    public void TestAPixelBufferIsSummarisedRatherThanSerialised()
    {
        var text = Logged("""
            const img = createCanvas(1600, 1200).getContext('2d').getImageData(0, 0, 1600, 1200);
            log(JSON.stringify(img));
            """);

        Assert.Equal("""{"width":1600,"height":1200,"byteLength":7680000}""", text);
    }

    /// <summary>
    /// Sized against the buffer rather than against a constant: the assertion has to fail if the
    /// pixels ever come back, and a fixed byte count would pass while quietly measuring nothing.
    /// </summary>
    [Fact]
    public void TestTheSerialisedFormIsOrdersOfMagnitudeSmallerThanTheBuffer()
    {
        var text = Logged("""
            const img = createCanvas(1600, 1200).getContext('2d').getImageData(0, 0, 1600, 1200);
            log(`${JSON.stringify(img).length} of ${img.data.length}`);
            """);

        var parts = text.Split(" of ");
        var serialised = int.Parse(parts[0]);
        var buffer = int.Parse(parts[1]);

        Assert.Equal(7_680_000, buffer);
        Assert.True(serialised < buffer / 10_000,
            $"{serialised} chars for a {buffer}-byte buffer — the pixels are being transcribed again");
    }

    /// <summary>The documented spelling has to survive a round trip, which is what it did not before.</summary>
    [Fact]
    public void TestTheDocumentedCamelCaseNamesSurviveJsonParse()
    {
        var text = Logged("""
            const img = createCanvas(4, 4).getContext('2d').getImageData(0, 0, 4, 4);
            const back = JSON.parse(JSON.stringify(img));
            log(`${back.width}x${back.height} ${back.byteLength} ${back.Width === undefined}`);
            """);

        Assert.Equal("4x4 64 true", text);
    }

    /// <summary>Serialising changes nothing about reading the real thing.</summary>
    [Fact]
    public void TestTheBufferItselfIsStillReachable()
    {
        var text = Logged("""
            const img = createCanvas(4, 4).getContext('2d').getImageData(0, 0, 4, 4);
            JSON.stringify(img);
            log(String(img.data.length));
            """);

        Assert.Equal("64", text);
    }

    /// <summary>
    /// <c>then</c> stays unconditional, and this is the guard on the one asymmetry in that change.
    /// </summary>
    /// <remarks>
    /// <c>await</c> probes <c>then</c> on every value it resolves, so a type that happened to carry a
    /// <c>Then</c> would hijack awaiting — a far worse failure than the one being fixed. So the
    /// exemption was narrowed for <c>toJSON</c> only, and this asserts the other half held.
    /// </remarks>
    [Fact]
    public void TestTheThenableProbeStillReadsAsAbsent()
    {
        var text = Logged("""
            const img = createCanvas(4, 4).getContext('2d').getImageData(0, 0, 4, 4);
            log(typeof img.then);
            """);

        Assert.Equal("undefined", text);
    }

    /// <summary>And awaiting a real SDK promise still works, which is what that exemption protects.</summary>
    [Fact]
    public void TestAwaitingStillWorks()
    {
        var result = Run("""
            const value = await Promise.resolve(7);
            log(String(value));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.Contains("7"));
    }

    /// <summary>A member that genuinely is not there still reads as undefined rather than throwing.</summary>
    [Fact]
    public void TestAnAbsentMemberIsStillUndefinedRatherThanAnError()
    {
        var text = Logged("""
            const img = createCanvas(4, 4).getContext('2d').getImageData(0, 0, 4, 4);
            log(typeof img.noSuchThing);
            """);

        Assert.Equal("undefined", text);
    }
    #endregion
}
