namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Interop;

/// <summary>
/// Carries the <c>Session</c> scratchpad across the boundary between a script and the run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The scratchpad has to outlive the script that writes it, and a
/// <c>JsValue</c> cannot: a new <see cref="Engine"/> is built per execution, so anything Jint owns
/// dies with the script. The durable half is therefore CLR — <c>SessionContext.Storage</c> — and
/// something has to translate.
/// </para>
/// <para>
/// <b>What was wrong with translating at the assignment.</b> <c>Storage</c> used to be handed to
/// Jint directly, so writing a plain value into it converted there and then: a JS array arrived as a
/// fixed-size <c>object[]</c>, a JS object as an <c>ExpandoObject</c>. The script's object and the
/// stored one were two objects from that moment, so
/// <c>Session.cast = cast; cast.push(...)</c> stored an empty array and the push went nowhere — and
/// a fixed-size array is also why <c>Session.cast.push(...)</c> could not grow one afterwards. A
/// live run lost a script to it, silently: the next script's loop ran zero times, logged nothing,
/// rendered a flat image and reported success.
/// </para>
/// <para>
/// <b>What changed.</b> Jint has nothing to convert when the target type is already
/// <c>JsValue</c>, so the map handed to the engine holds exactly what the script holds — one object,
/// not two — and translation happens **once, at the end of the execution**, by which time every
/// mutation the script made is part of what is being translated. That is the whole fix, and it is a
/// change of value type rather than a new mechanism: a <c>Dictionary&lt;string, JsValue&gt;</c> is
/// still an <c>IDictionary</c>, so <c>delete Session.x</c>, <c>has(Session, 'x')</c> and an absent
/// key reading as <c>undefined</c> all keep working through Jint's own dictionary path.
/// </para>
/// <para>
/// The conversion at the boundary is genuinely unavoidable and is not a defect to be fixed later;
/// what was avoidable was doing it three statements too early. <c>SessionMarshallingTests</c> pins
/// both halves.
/// </para>
/// <para>
/// <b>A function is the one value that cannot be translated</b>, because it is code bound to the engine
/// that compiled it: its closure, its globals and its constraints all live there. So it is kept with
/// that engine, and each call from a later script first resets the home engine's constraints. Without
/// the reset a stored function runs on the clock of the script that <i>defined</i> it, and every call
/// made more than one script-timeout after that throws <c>The operation has timed out.</c> — which a
/// live run spent fourteen minutes attributing to the network. <c>SessionFunctionClockTests</c> pins it.
/// </para>
/// </remarks>
internal static class SessionBridge
{
    #region Types
    /// <summary>A function kept in the scratchpad, with the engine it belongs to.</summary>
    internal sealed record StoredFunction(Engine Home, Function Code);
    #endregion

    #region Fields
    /// <summary>The wrapper each <see cref="StoredFunction"/> was given, so settling it again keeps the original.</summary>
    static readonly ConditionalWeakTable<JsValue, StoredFunction> wrappers = new();
    #endregion

    #region Methods
    /// <summary>The scratchpad as the engine should see it: live values, rehydrated.</summary>
    /// <remarks>
    /// <c>FromObject</c> is what makes a stored array usable again — it returns a real JS array
    /// rather than a wrapper over the fixed-size one that was settled, so the next script can push
    /// to what the last one left. An SDK object needs no rehydration and is wrapped, which is what
    /// keeps a stashed bitmap the same bitmap.
    /// </remarks>
    public static Dictionary<string, JsValue> Open(Engine engine, IDictionary<string, object?> settled)
    {
        var live = new Dictionary<string, JsValue>(settled.Count, StringComparer.Ordinal);
        foreach (var (key, value) in settled)
        {
            // One unreadable entry must not cost the whole scratchpad: a script that stored
            // something exotic last time should lose that key, not every key.
            try { live[key] = value is StoredFunction stored ? Wrap(engine, stored) : JsValue.FromObject(engine, value); }
            catch (Exception ex) { Runtime.Warn("Session key '{0}' could not be restored: {1}", key, ex.Message); }
        }

        return live;
    }

    /// <summary>Writes the script's scratchpad back, translating once, at the end.</summary>
    /// <remarks>
    /// Replaces rather than merges, so a <c>delete Session.x</c> during the run is carried through
    /// instead of leaving the old value standing — the live map is the whole truth by this point.
    /// </remarks>
    public static void Settle(Engine? engine, Dictionary<string, JsValue>? live, IDictionary<string, object?> settled)
    {
        if (live is null) return;

        settled.Clear();
        foreach (var (key, value) in live)
        {
            try
            {
                settled[key] = wrappers.TryGetValue(value, out var kept) ? kept
                    : value is Function code && engine is not null ? new StoredFunction(engine, code)
                    : value.ToObject();
            }
            catch (Exception ex) { Runtime.Warn("Session key '{0}' could not be kept: {1}", key, ex.Message); }
        }
    }

    /// <summary>A stored function as the calling engine sees it.</summary>
    /// <remarks>
    /// Arguments cross as CLR values and are rebuilt in the home engine, and the result comes back the
    /// same way — which is what the delegate this replaces did, so an SDK object passes through as
    /// itself. The reset gives each call the home engine's full limits; the caller's own limit is
    /// checked again when control returns, so a call that never returns is still stopped.
    /// </remarks>
    static JsValue Wrap(Engine engine, StoredFunction stored)
    {
        var wrapper = new ClrFunction(engine, "stored", (_, args) =>
        {
            stored.Home.Constraints.Reset();
            var homeArgs = new JsValue[args.Length];
            for (var i = 0; i < args.Length; i++) homeArgs[i] = JsValue.FromObject(stored.Home, args[i].ToObject());
            return JsValue.FromObject(engine, stored.Code.Call(JsValue.Undefined, homeArgs).ToObject());
        });
        wrappers.AddOrUpdate(wrapper, stored);
        return wrapper;
    }
    #endregion
}
