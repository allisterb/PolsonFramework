namespace Polson.MCPServer;

using System;

using Jint;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

/// <summary>
/// Keeps the "did you mean" message when a member that does not exist is <em>called</em>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MemberIndex"/> lets an unknown member read as <c>undefined</c>, so
/// <c>typeof ctx.drawLoomisWireframe</c> answers instead of throwing. Left alone, the cost of that is
/// the error a script gets when it calls one: Jint reports <i>"Property 'x' of object is not a
/// function"</i>, naming the member and nothing else — no type, no suggestion. The suggester was most
/// of the value of throwing in the first place.
/// </para>
/// <para>
/// <c>TryGetCallable</c> is the hook that gets it back. It fires on the call path with a
/// <see cref="Reference"/> carrying both the referenced name and the object it was read from, which
/// is exactly what <see cref="JsDrawingEngine.ExplainMissingMember"/> needs. Neither the member
/// accessor nor <c>TypeResolver</c> can see this — they are handed a name and a target with no idea
/// what the surrounding expression is doing, which is why <c>typeof</c> could not be accommodated
/// there and had to be accommodated here instead.
/// </para>
/// <para>
/// Only the call path is intercepted. A missing member that is read, tested or passed around stays
/// <c>undefined</c>, because that is the whole point — but every such read is recorded, so a script
/// quietly building on nothing is visible in the run rather than merely possible to notice.
/// </para>
/// </remarks>
internal sealed class MissingCallResolver : IReferenceResolver
{
    #region Methods
    public bool TryGetCallable(Engine engine, object callee, out JsValue value)
    {
        value = JsValue.Undefined;

        if (callee is not Reference reference) return false;

        var member = reference.ReferencedName?.ToString();
        var target = reference.Base is ObjectWrapper wrapper ? wrapper.Target : null;

        // Not ours to explain: a plain JS object, or a call on something with no CLR type behind it.
        // Returning false lets Jint report it in its own words, which for ordinary JavaScript are the
        // right words.
        if (target is null || string.IsNullOrEmpty(member)) return false;

        // The message Jint would have produced had it refused the read, so one explainer serves the
        // call, the assignment and `suggest(...)` rather than three that drift apart.
        var explained = JsDrawingEngine.ExplainMissingMember(
            $"Cannot access property '{member}' on type '{target.GetType().FullName}'");

        throw new JavaScriptException(engine.Intrinsics.TypeError, explained);
    }

    /// <summary>Not ours. Jint's own handling of an unresolvable identifier is correct.</summary>
    public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
    {
        value = JsValue.Undefined;
        return false;
    }

    public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value) => false;

    public bool CheckCoercible(JsValue value) => false;
    #endregion
}
