namespace Polson.Tests.MCPServer;

using System.Collections.Generic;

using global::Polson.Tests;

using Jint;
using Jint.Native;

using Xunit;

/// <summary>
/// The four things Jint does with a <c>Dictionary&lt;string, JsValue&gt;</c> that
/// <see cref="global::Polson.MCPServer.SessionBridge"/> is built on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kept as a separate file because these are assertions about Jint, not about us.</b>
/// <c>SessionMarshallingTests</c> asserts what a script author sees; if the interop contract
/// underneath ever changes — a Jint upgrade, a different type converter — those tests would fail in
/// several places at once and none of the messages would name the cause. These would fail in exactly
/// one, and say what moved.
/// </para>
/// <para>
/// The load-bearing one is the first: Jint has nothing to convert when the target type is already a
/// <c>JsValue</c>, which is the entire reason the scratchpad can hold what a script is holding
/// rather than a translation of it.
/// </para>
/// </remarks>
public class JintDictionaryContractTests : TestsRuntime
{
    #region Contract Tests
    /// <summary>A JsValue-valued dictionary stores the script's own object, not a conversion of it.</summary>
    [Fact]
    public void AJsValueDictionaryKeepsTheReference()
    {
        var store = new Dictionary<string, JsValue>();
        var engine = new Engine();
        engine.SetValue("Session", store);

        engine.Execute("const a = []; Session.a = a; a.push('x'); a.push('y');");

        Assert.Equal(2u, store["a"].AsArray().Length);
    }

    /// <summary>An object-valued one converts instead, which is what the scratchpad used to be.</summary>
    /// <remarks>
    /// The contrast is the point: the conversion was never forced by the boundary, only by the
    /// value type on the other side of it.
    /// </remarks>
    [Fact]
    public void AnObjectDictionaryConvertsOnAssignment()
    {
        var store = new Dictionary<string, object?>();
        var engine = new Engine();
        engine.SetValue("Session", store);

        engine.Execute("const a = []; Session.a = a; a.push('x');");

        Assert.Equal(typeof(object[]), store["a"]!.GetType());
        Assert.Empty((object[])store["a"]!);
    }

    /// <summary>`delete` reaches the dictionary, so a removal can be carried across executions.</summary>
    [Fact]
    public void DeleteRemovesTheKey()
    {
        var store = new Dictionary<string, JsValue>();
        var engine = new Engine();
        engine.SetValue("Session", store);

        engine.Execute("Session.a = 1; delete Session.a;");

        Assert.False(store.ContainsKey("a"));
    }

    /// <summary>A settled CLR array rehydrates as a JS array that can still grow.</summary>
    /// <remarks>
    /// Without this the fix would be half a fix: the assignment would keep the reference and the
    /// <i>next</i> script would still be pushing at a fixed-size wrapper.
    /// </remarks>
    [Fact]
    public void FromObjectRehydratesAGrowableArray()
    {
        var engine = new Engine();
        var store = new Dictionary<string, JsValue>
        {
            ["cast"] = JsValue.FromObject(engine, new object?[] { "a", "b" }),
        };
        engine.SetValue("Session", store);

        engine.Execute("Session.cast.push('c');");

        Assert.Equal(3u, store["cast"].AsArray().Length);
    }
    #endregion
}
