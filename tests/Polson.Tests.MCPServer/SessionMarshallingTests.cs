namespace Polson.Tests.MCPServer;

using System;

using global::Polson.MCPServer;
using global::Polson.Tests;

using Xunit;

/// <summary>
/// What crossing the <c>Session</c> boundary does to a value, measured rather than assumed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because a live run lost a script to this, and the first explanation offered for it was
/// wrong.</b> A storyboard run wrote <c>Session.cast = []</c>, pushed into the array, and found a
/// zero-length array in the next execution — silently: the next script's loop ran zero times, logged
/// nothing, rendered a flat image and reported success.
/// </para>
/// <para>
/// The explanation offered was "the stored value is snapshotted at assignment", and that cannot be
/// right as stated: <c>SessionContext.Storage</c> is a dictionary of <c>object?</c>, and .NET does
/// not copy a reference type on assignment. The real mechanism, which these tests measure rather
/// than reason about, is that <b>Jint converts at the boundary</b> — a <c>JsValue</c> written into a
/// CLR dictionary is marshalled through <c>ToObject()</c>, and that <i>allocates</i>: a JS array
/// arrives as <see cref="object"/><c>[]</c>, a JS object as a
/// <see cref="System.Dynamic.ExpandoObject"/>. The script's object and the stored one are therefore
/// two objects, and no amount of reference semantics connects them.
/// </para>
/// <para>
/// <b>The distinction that makes the rule usable:</b> an SDK object is <i>already</i> CLR, so there
/// is nothing to convert and it really is shared — which is what the reference's advice to stash a
/// bitmap between stages depends on. Plain JS values are converted; SDK objects are not.
/// </para>
/// </remarks>
public class SessionMarshallingTests : TestsRuntime
{
    #region Conversion Tests
    /// <summary>A JS array crosses as a fixed-size CLR array, which is the whole explanation.</summary>
    [Fact]
    public void AJsArrayIsConvertedToAClrArray()
    {
        var session = new SessionContext();

        Run(session, "Session.cast = ['a', 'b'];");

        Assert.Equal(typeof(object[]), session.Storage["cast"]!.GetType());
    }

    /// <summary>A JS object crosses as an ExpandoObject, for the same reason.</summary>
    [Fact]
    public void AJsObjectIsConvertedToAnExpando()
    {
        var session = new SessionContext();

        Run(session, "Session.c = { name: 'a' };");

        Assert.Equal(typeof(System.Dynamic.ExpandoObject), session.Storage["c"]!.GetType());
    }

    /// <summary>
    /// An SDK object is not converted, so a change made to it in one script is there in the next.
    /// </summary>
    /// <remarks>
    /// Asserted through behaviour rather than through the stored type, because the type would look
    /// right even if a copy had been taken. This is the half that keeps the reference's "stash the
    /// bitmap rather than encoding it" advice true, and it is the opposite result to every test
    /// above — which is exactly why the rule has to name the two cases separately.
    /// </remarks>
    [Fact]
    public void AnSdkObjectCrossesByReferenceAndKeepsItsChanges()
    {
        var session = new SessionContext();

        Run(session, "Session.plate = createCanvas(4, 4).toBitmap(); "
                   + "Session.plate.setPixel(0, 0, '#FF0000FF');");

        Assert.Equal("#FF0000FF", Logged(Run(session, "log(Session.plate.getPixel(0, 0));")));
    }
    #endregion

    #region Consequence Tests
    /// <summary>The live failure, reproduced: the push does not reach the stored array.</summary>
    [Fact]
    public void MutatingAnArrayAfterStoringItDoesNotPersist()
    {
        var session = new SessionContext();

        Run(session, "const cast = []; Session.cast = cast; cast.push('a'); cast.push('b');");

        Assert.Equal("0", Logged(Run(session, "log(String(Session.cast.length));")));
    }

    /// <summary>Nor does pushing through the stored reference — a CLR array cannot grow.</summary>
    [Fact]
    public void PushingThroughTheStoredReferenceDoesNotPersistEither()
    {
        var session = new SessionContext();

        Run(session, "Session.cast = []; Session.cast.push('a');");

        Assert.Equal("0", Logged(Run(session, "log(String(Session.cast.length));")));
    }

    /// <summary>A property added to a stored object is likewise invisible afterwards.</summary>
    [Fact]
    public void APropertyAddedAfterStoringAnObjectDoesNotPersist()
    {
        var session = new SessionContext();

        Run(session, "const c = { name: 'a' }; Session.c = c; c.uri = 'data:,x';");

        Assert.Equal("undefined", Logged(Run(session, "log(String(Session.c.uri));")));
    }

    /// <summary>Assigning the finished value in one go is the spelling that works.</summary>
    [Fact]
    public void AValueAssignedOnceSurvivesWhole()
    {
        var session = new SessionContext();

        Run(session, "Session.cast = ['a', 'b'].map(n => ({ name: n }));");

        Assert.Equal("2,b", Logged(Run(session,
            "log(Session.cast.length + ',' + Session.cast[1].name);")));
    }

    /// <summary>Reassigning after building works too, and is the remedy for a loop that fills.</summary>
    [Fact]
    public void ReassigningAfterBuildingSurvives()
    {
        var session = new SessionContext();

        Run(session, "const cast = []; cast.push('a'); cast.push('b'); Session.cast = cast;");

        Assert.Equal("2", Logged(Run(session, "log(String(Session.cast.length));")));
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
        var at = last.LastIndexOf("] ", StringComparison.Ordinal);
        return (at >= 0 ? last[(at + 2)..] : last).Trim();
    }
    #endregion
}
