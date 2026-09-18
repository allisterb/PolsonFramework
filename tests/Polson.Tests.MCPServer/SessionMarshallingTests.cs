namespace Polson.Tests.MCPServer;

using System;

using global::Polson.MCPServer;
using global::Polson.Tests;

using Xunit;

/// <summary>
/// What crossing the <c>Session</c> boundary does to a value.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because a live run lost a script, and rewritten because the first explanation was
/// wrong.</b> A storyboard run wrote <c>Session.cast = []</c>, pushed into the array, and found a
/// zero-length array in the next execution — silently: the next script's loop ran zero times, logged
/// nothing, rendered a flat image and reported success.
/// </para>
/// <para>
/// The explanation offered was "the stored value is snapshotted at assignment". That was the right
/// observation and the wrong diagnosis: .NET does not copy a reference type on assignment, so
/// something had to be <i>converting</i>. It was — the engine was handed the durable CLR store
/// directly, so a JS array became a fixed-size <c>object[]</c> the moment it was written, and the
/// script's array and the stored one were two objects from then on.
/// </para>
/// <para>
/// <b>Which made it fixable, and it is fixed.</b> Conversion at the <i>execution</i> boundary is
/// forced — a new engine per execution means no <c>JsValue</c> survives — but conversion at the
/// moment of <i>assignment</i> was only the container we chose. The engine now gets a map of live
/// <c>JsValue</c> and <see cref="SessionBridge"/> translates once, at the end. These tests assert
/// the behaviour a script author sees; the three that used to record the loss now record that it
/// does not happen.
/// </para>
/// </remarks>
public class SessionMarshallingTests : TestsRuntime
{
    #region Reference Tests
    /// <summary>The live failure, now the other way round: the push reaches the scratchpad.</summary>
    [Fact]
    public void MutatingAnArrayAfterStoringItPersists()
    {
        var session = new SessionContext();

        Run(session, "const cast = []; Session.cast = cast; cast.push('a'); cast.push('b');");

        Assert.Equal("2", Logged(Run(session, "log(String(Session.cast.length));")));
    }

    /// <summary>And pushing through the stored reference works, because it is a real array.</summary>
    /// <remarks>
    /// The second symptom of the same defect, and it needed the other half of the fix: what is
    /// settled has to rehydrate as a JS array rather than a wrapper over a fixed-size CLR one, or
    /// this grows nothing however live the assignment was.
    /// </remarks>
    [Fact]
    public void PushingThroughTheStoredReferencePersists()
    {
        var session = new SessionContext();

        Run(session, "Session.cast = []; Session.cast.push('a');");

        Assert.Equal("1", Logged(Run(session, "log(String(Session.cast.length));")));
    }

    /// <summary>A property added to a stored object is there afterwards too.</summary>
    [Fact]
    public void APropertyAddedAfterStoringAnObjectPersists()
    {
        var session = new SessionContext();

        Run(session, "const c = { name: 'a' }; Session.c = c; c.uri = 'data:,x';");

        Assert.Equal("data:,x", Logged(Run(session, "log(String(Session.c.uri));")));
    }

    /// <summary>An array grown across three executions keeps everything, which is the real use.</summary>
    /// <remarks>
    /// The shape a cast sheet is actually built in — requisition, append, append — and the one that
    /// exercises both halves at once: each execution rehydrates what the last settled, and settles
    /// what this one added.
    /// </remarks>
    [Fact]
    public void AnArrayGrownAcrossExecutionsKeepsEverything()
    {
        var session = new SessionContext();

        Run(session, "Session.cast = ['a'];");
        Run(session, "Session.cast.push('b');");
        Run(session, "const c = Session.cast; c.push('c');");

        Assert.Equal("3,c", Logged(Run(session,
            "log(Session.cast.length + ',' + Session.cast[2]);")));
    }
    #endregion

    #region SDK Object Tests
    /// <summary>
    /// An SDK object is shared, which is what the bitmap-stashing advice in the reference rests on.
    /// </summary>
    /// <remarks>
    /// Asserted through behaviour rather than through a stored type, because the type would look
    /// right even if a copy had been taken. This passed before the change and has to keep passing
    /// after it: the fix must not turn a shared native object into a translated one.
    /// </remarks>
    [Fact]
    public void AnSdkObjectCrossesByReferenceAndKeepsItsChanges()
    {
        var session = new SessionContext();

        Run(session, "Session.plate = createCanvas(4, 4).toBitmap(); "
                   + "Session.plate.setPixel(0, 0, '#FF0000FF');");

        Assert.Equal("#FF0000FF", Logged(Run(session, "log(Session.plate.getPixel(0, 0));")));
    }

    /// <summary>A bitmap survives a second hop, so a three-stage run can keep handing it on.</summary>
    [Fact]
    public void AnSdkObjectSurvivesMoreThanOneHop()
    {
        var session = new SessionContext();

        Run(session, "Session.plate = createCanvas(4, 4).toBitmap();");
        Run(session, "Session.plate.setPixel(1, 1, '#00FF00FF');");

        Assert.Equal("#00FF00FF", Logged(Run(session, "log(Session.plate.getPixel(1, 1));")));
    }
    #endregion

    #region Scratchpad Semantics Tests
    /// <summary>A key never set still reads as undefined rather than throwing.</summary>
    [Fact]
    public void AnAbsentKeyReadsAsUndefined() =>
        Assert.Equal("undefined", Logged(Run(new SessionContext(),
            "log(typeof Session.neverSet);")));

    /// <summary>`delete` still removes a key, and the removal is what gets kept.</summary>
    /// <remarks>
    /// The case that decides whether settling may merge: it may not. The live map is the whole
    /// truth at the end of a script, so a key deleted during it has to be gone rather than restored
    /// from what was there before.
    /// </remarks>
    [Fact]
    public void DeletingAKeyIsKept()
    {
        var session = new SessionContext();

        Run(session, "Session.gone = 'x';");
        Run(session, "delete Session.gone;");

        Assert.Equal("undefined", Logged(Run(session, "log(typeof Session.gone);")));
    }

    /// <summary>`has` still answers for keys, which is what makes the scratchpad askable.</summary>
    [Fact]
    public void HasStillAnswersForKeys()
    {
        var session = new SessionContext();

        Run(session, "Session.palette = ['#000'];");

        Assert.Equal("true,false", Logged(Run(session,
            "log(has(Session, 'palette') + ',' + has(Session, 'nope'));")));
    }

    /// <summary>A plain value written once is readable whole, as it always was.</summary>
    [Fact]
    public void AValueAssignedOnceSurvivesWhole()
    {
        var session = new SessionContext();

        Run(session, "Session.cast = ['a', 'b'].map(n => ({ name: n }));");

        Assert.Equal("2,b", Logged(Run(session,
            "log(Session.cast.length + ',' + Session.cast[1].name);")));
    }
    #endregion

    #region Durability Tests
    /// <summary>
    /// A script that fails still keeps what it wrote before failing.
    /// </summary>
    /// <remarks>
    /// Settling happens after the catches rather than on the success path, and this is why: the
    /// eager conversion used to make partial work durable for free, and a fix that quietly dropped
    /// the scratchpad whenever a script threw would be a worse trade than the defect it repaired.
    /// </remarks>
    [Fact]
    public void AFailedScriptKeepsWhatItWroteFirst()
    {
        var session = new SessionContext();

        var failed = Run(session, "Session.kept = 'yes'; throw new Error('stop');");

        Assert.False(failed.Success);
        Assert.Equal("yes", Logged(Run(session, "log(String(Session.kept));")));
    }

    /// <summary>What survives the round trip, and the two things that do not.</summary>
    /// <remarks>
    /// <para>
    /// <b>Written to check a limit the reference claimed, and the claim was false.</b> The
    /// documentation said a function could not survive because "there is nothing on the other side
    /// for one to become" - which sounded right and had never been run. A function settles as a
    /// delegate and rehydrates <i>callable</i>: <c>Session.f(21)</c> returns 42 in the next script.
    /// </para>
    /// <para>
    /// <b>The real edge is elsewhere, and it is narrow: a <c>Map</c> or a <c>Set</c> comes back as a
    /// plain object.</b> Its entries are not lost, but its identity is - <c>.get</c> and <c>.has</c>
    /// are gone, and the failure is the quiet kind, because a plain object is still truthy and still
    /// has properties. <c>Date</c> and arbitrary nesting are fine.
    /// </para>
    /// <para>
    /// Worth recording as a table rather than a rule, because a guess about which of these five
    /// survives is exactly what produced the wrong claim the first time.
    /// </para>
    /// </remarks>
    [Fact]
    public void WhatSurvivesTheRoundTrip()
    {
        var session = new SessionContext();

        Run(session, "Session.f = (n) => n * 2; Session.m = new Map([['a', 1]]); "
                   + "Session.s = new Set([1, 2]); Session.d = new Date(0); "
                   + "Session.n = { a: { b: [1, 2] } };");

        // Kept whole.
        Assert.Equal("function", Logged(Run(session, "log(typeof Session.f);")));
        Assert.Equal("42", Logged(Run(session, "log(String(Session.f(21)));")));
        Assert.Equal("[object Date]",
            Logged(Run(session, "log(Object.prototype.toString.call(Session.d));")));
        Assert.Equal("{\"a\":{\"b\":[1,2]}}",
            Logged(Run(session, "log(JSON.stringify(Session.n));")));

        // Flattened: the collection arrives as an ordinary object.
        Assert.Equal("[object Object]",
            Logged(Run(session, "log(Object.prototype.toString.call(Session.m));")));
        Assert.Equal("[object Object]",
            Logged(Run(session, "log(Object.prototype.toString.call(Session.s));")));
    }

    /// <summary>The durable store is CLR afterwards, because it has to outlive the engine.</summary>
    /// <remarks>
    /// The one assertion here about mechanism rather than behaviour, and it earns its place: it
    /// states the constraint the whole design is shaped by. A <c>JsValue</c> belongs to its engine
    /// and a new engine is built per execution, so what persists cannot be one.
    /// </remarks>
    [Fact]
    public void WhatPersistsBetweenExecutionsIsClr()
    {
        var session = new SessionContext();

        Run(session, "Session.cast = ['a', 'b'];");

        Assert.Equal(typeof(object[]), session.Storage["cast"]!.GetType());
    }
    #endregion

    #region Helpers
    static DrawingExecutionResult Run(SessionContext session, string script) =>
        new JsDrawingEngine().Execute(script, 20, 20, session, "png", 90, render: false);

    /// <summary>The last logged line, which is what these scripts report through.</summary>
    static string Logged(DrawingExecutionResult result)
    {
        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(result.Logs);
        var last = result.Logs[^1];
        var at = last.IndexOf("] ", StringComparison.Ordinal);
        return (at >= 0 ? last[(at + 2)..] : last).Trim();
    }
    #endregion
}
