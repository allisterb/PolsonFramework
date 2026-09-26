namespace Polson.MCPServer;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Backs the <c>has(object, name)</c> global: whether a member exists, without touching it.
/// </summary>
/// <remarks>
/// <para>
/// A script cannot ask this in ordinary JavaScript. Resolution is strict here — a member that is not
/// there throws rather than reading as <c>undefined</c>, which is what stops
/// <c>ctx.fillStlye = 'red'</c> from silently doing nothing — and that same strictness defeats every
/// idiom for asking: <c>typeof ctx.foo</c>, <c>'foo' in ctx</c>, <c>Object.hasOwn(ctx, 'foo')</c> and
/// <c>Reflect.has(ctx, 'foo')</c> all go through the member accessor and all throw. <c>Object.keys</c>
/// does not list methods, so it cannot answer either.
/// </para>
/// <para>
/// Relaxing resolution to accommodate <c>typeof</c> was tried and rejected. It works — an existence
/// check in the accessor plus <c>IReferenceResolver.TryGetCallable</c> keeps the "did you mean"
/// message on the call path — but a misspelled *read* then goes silent, and a chained one
/// (<c>Skia.RuntimeEffect.make()</c>) loses its explanation entirely, because by then the base is
/// plain <c>undefined</c> and remembers nothing. That is the class of bug strict resolution exists to
/// kill, so the probe is a function instead of a semantics change.
/// </para>
/// <para>
/// <b>Why it is worth having.</b> Without it an agent must either write a call it cannot verify into
/// a 50 KB script, or spend one whole execution per candidate name deleting and re-running. A live
/// run spent eighteen of its seventy-one renders doing exactly that.
/// </para>
/// <para>
/// <b>Cost.</b> Names are read once per type and cached, so a query is a dictionary lookup plus a set
/// lookup. Nothing on the drawing path consults this at all.
/// </para>
/// </remarks>
internal static class MemberIndex
{
    #region Methods
    /// <summary>
    /// Whether <paramref name="member"/> resolves on <paramref name="target"/>.
    /// </summary>
    /// <remarks>
    /// A dictionary is asked about its <em>keys</em> rather than its type. <c>Session</c> is a
    /// <c>Dictionary&lt;string, object&gt;</c> and the SDK documents it as an ordinary scratchpad, so
    /// judging it by its CLR members would answer "no" for every key a script had just set — turning
    /// the one deliberately dynamic object in the sandbox into the one that could not be read.
    /// </remarks>
    internal static bool Has(object? target, string member)
    {
        if (target is null || string.IsNullOrEmpty(member)) return false;

        // A dictionary is answered from its keys and then settled, never falling through to the
        // collection rule below. `Session` is a dictionary *and* an IEnumerable, so deferring would
        // make `has(Session, anything)` true — the scratchpad is the one object whose contents are
        // fully knowable, and it would have been the one that could not be asked about.
        if (target is IDictionary dictionary)
        {
            return dictionary.Contains(member) || NamesOf(target.GetType()).Contains(member);
        }

        if (NamesOf(target.GetType()).Contains(member)) return true;

        // A collection resolves more than its CLR type carries: Jint attaches the JS array prototype
        // to a wrapped list, so `paper.selectAll('rect').filter(...)` is answered by `Array.prototype`
        // and appears nowhere in `GetMembers`. This indexes what the *type* declares, so on a
        // collection it cannot tell "absent" from "attached".
        //
        // It answers `true` rather than guessing. A false "no" would send a script down a fallback
        // path away from a call that works, which is a worse error than the uncertainty it reports —
        // and the caller can always just try it, since a real absence still throws informatively.
        return target is IEnumerable and not string;
    }

    /// <summary>
    /// Whether <paramref name="target"/>'s type itself declares <paramref name="member"/>, in either
    /// spelling. Unlike <see cref="Has"/> it never answers yes for a collection on principle.
    /// </summary>
    internal static bool Declares(object target, string member) => NamesOf(target.GetType()).Contains(member);

    /// <summary>Every name a script could legitimately use on this type, in both spellings.</summary>
    /// <remarks>
    /// Jint resolves the JS camelCase spelling onto a PascalCase .NET member, so both are indexed:
    /// asking about <c>fillRect</c> has to find <c>FillRect</c>. Indexing both spellings rather than
    /// normalising the query keeps the lookup a plain set membership test.
    /// <para>
    /// <b>Static members count.</b> Jint reaches them through the instance, and the SDK documents
    /// several that way — <c>Assets.classify(...)</c> is a static on the toolkit instance the script
    /// holds. Scanning instance members alone made every one of them read as <c>undefined</c>, which
    /// is a far worse failure than the one this class exists to remove: not an unhelpful error, but a
    /// documented call quietly becoming nothing.
    /// </para>
    /// </remarks>
    private static HashSet<string> NamesOf(Type type) => Cache.GetOrAdd(type, static t =>
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in t.GetMembers(BindingFlags.Public | BindingFlags.Instance
                                          | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        {
            // The same exclusions the symbol manifest and the "did you mean" suggester apply, so the
            // three agree on what the surface is. Without it `has(ctx, 'getType')` answers true for a
            // member the reference deliberately does not document — a probe whose whole purpose is
            // deciding what to write must not point at something no script should call.
            if (JsSurface.NotSurface.Contains(member.Name)) continue;
            if (member.Name.StartsWith("op_", StringComparison.Ordinal)) continue;

            names.Add(member.Name);

            if (member.Name.Length > 0 && char.IsUpper(member.Name[0]))
            {
                names.Add(char.ToLowerInvariant(member.Name[0]) + member.Name[1..]);
            }
        }

        return names;
    });
    #endregion

    #region Fields
    /// <summary>
    /// Keyed by type, so the reflection happens once per type per process rather than per access.
    /// </summary>
    /// <remarks>
    /// Concurrent because scripts run on the thread pool: <c>ExecuteScript</c> hands the engine to
    /// <c>Task.Run</c>, and two sessions can be in the same toolkit type at once.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, HashSet<string>> Cache = new();
    #endregion
}
