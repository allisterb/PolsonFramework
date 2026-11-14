namespace Polson.MCPServer;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

using Jint;
using Jint.Native;
using Jint.Runtime.Interop;

/// <summary>
/// <c>JSON.stringify</c> for a result record with no <c>ToJSON</c> of its own: its public properties under their
/// documented camelCase names.
/// </summary>
/// <remarks>
/// <para>
/// Left to Jint, the stringify walks the CLR object under its PascalCase names, so a logged result reads
/// <c>{"Success":true,"Text":…}</c> while every doc says <c>success</c> and <c>text</c>. Agents take the log as the
/// truth and conclude the documented member is missing (lanternkeeperlang1 and 2, <c>Documents.ask</c>).
/// </para>
/// <para>
/// <b>Records only.</b> They are the result types, and they are not cyclic. A general class graph can be — an element
/// holds its paper, which holds the element — and a hook that answers with a fresh object each time hides the cycle
/// from <c>JSON.stringify</c>, which then recurses until the stack overflows and takes the server with it. A class
/// that wants this writes its own <c>ToJSON</c>, as <c>SnapBBox</c> and <c>ImageData</c> do.
/// </para>
/// </remarks>
internal static class RecordJson
{
    #region Methods
    /// <summary>A C# record, which the compiler marks with a <c>&lt;Clone&gt;$</c> method.</summary>
    public static bool Applies(Type type) => Properties.GetOrAdd(type, Readable) is { Length: > 0 };

    /// <summary>The <c>toJSON</c> function for one record: a plain object whose values the stringify then walks in turn.</summary>
    public static JsValue Hook(Engine engine, object target) => new ClrFunction(engine, "toJSON", (_, _) =>
    {
        var result = new JsObject(engine);
        foreach (var p in Properties[target.GetType()])
        {
            result.Set(Camel(p.Name), JsValue.FromObject(engine, p.GetValue(target)));
        }

        return result;
    });

    private static PropertyInfo[] Readable(Type type) =>
        type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is null
            ? []
            : type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToArray();

    private static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    #endregion

    #region Fields
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();
    #endregion
}
