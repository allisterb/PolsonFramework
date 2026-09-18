namespace Polson.Tests.MCPServer;

using System.Collections.Generic;

using global::Polson.Tests;

using Jint;
using Jint.Native;

using Xunit;

/// <summary>
/// A throwaway probe: would a scratchpad that stores <c>JsValue</c> keep reference semantics?
/// </summary>
/// <remarks>
/// <para>
/// Not a test of anything shipped. <c>Session</c> is a <c>Dictionary&lt;string, object?&gt;</c>, so
/// assigning a plain JS value converts it and later mutation is lost —
/// <c>SessionMarshallingTests</c> measures that. The question this answers is whether the conversion
/// is forced by the boundary or merely by the container we chose to put behind it.
/// </para>
/// <para>
/// The distinction matters because a new <c>Engine</c> is built per execution, so a <c>JsValue</c>
/// cannot outlive the script that made it: conversion at the <i>execution</i> boundary is
/// unavoidable. What is avoidable is converting at the moment of <i>assignment</i>, which is where
/// the live failure happened — store then push, both inside one script.
/// </para>
/// </remarks>
public class ScratchpadProbeTests : TestsRuntime
{
    #region Probe Tests
    /// <summary>The shipped shape: a CLR dictionary converts on the way in, so the push is lost.</summary>
    [Fact]
    public void ADictionaryOfObjectConvertsOnAssignment()
    {
        var store = new Dictionary<string, object?>();
        var engine = new Engine();
        engine.SetValue("Session", store);

        engine.Execute("const a = []; Session.a = a; a.push('x');");

        Assert.Equal(typeof(object[]), store["a"]!.GetType());
        Assert.Empty((object[])store["a"]!);
    }

    /// <summary>
    /// A holder whose indexer takes <c>JsValue</c> keeps the script's own object, so the push lands.
    /// </summary>
    /// <remarks>
    /// This is the finding. Jint has no conversion to do when the target type is already
    /// <c>JsValue</c>, so what is stored is the same array the script is holding — one object, not
    /// two — and mutation after assignment is visible exactly as ordinary JavaScript would have it.
    /// </remarks>
    [Fact]
    public void AHolderTypedAsJsValueKeepsTheReference()
    {
        var store = new LiveScratchpad();
        var engine = new Engine();
        engine.SetValue("Session", store);

        engine.Execute("const a = []; Session.a = a; a.push('x'); a.push('y');");

        Assert.Equal(2u, store["a"].AsArray().Length);
    }

    /// <summary>And it converts on demand, which is what the execution boundary would call.</summary>
    /// <remarks>
    /// The other half of the design: whatever is held live has to become CLR before the engine that
    /// owns it is discarded. Converting at that point captures every mutation made during the run,
    /// which is the whole difference from converting at assignment.
    /// </remarks>
    [Fact]
    public void WhatIsHeldLiveConvertsWhenAsked()
    {
        var store = new LiveScratchpad();
        var engine = new Engine();
        engine.SetValue("Session", store);

        engine.Execute("const a = []; Session.a = a; a.push('x'); a.push('y');");
        var settled = store["a"].ToObject();

        Assert.Equal(typeof(object[]), settled!.GetType());
        Assert.Equal(2, ((object[])settled).Length);
    }
    #endregion

    #region Types
    /// <summary>A scratchpad that stores what the script is holding rather than a copy of it.</summary>
    sealed class LiveScratchpad
    {
        readonly Dictionary<string, JsValue> held = [];

        public JsValue this[string key]
        {
            get => held.TryGetValue(key, out var value) ? value : JsValue.Undefined;
            set => held[key] = value;
        }
    }
    #endregion
}
