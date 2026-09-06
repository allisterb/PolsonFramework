namespace Polson;

using System.Collections;
using System.Collections.Generic;

/// <summary>Normalises loosely-typed values arriving from the JavaScript sandbox.</summary>
/// <remarks>
/// In <c>Polson.Runtime</c> and in the root namespace rather than in a drawing project, because both
/// surfaces need it: the raster toolkits take options objects, and the vector chart drawer reads the
/// same closed-form models onto a <c>SnapPaper</c>. C# resolves up the namespace chain, so every
/// <c>Polson.Drawing.*</c> file sees it without a using directive and no call site changed when it moved.
/// </remarks>
public static class JsInterop
{
    #region Methods
    /// <summary>
    /// Views a value as a non-generic dictionary, whatever shape it arrived in.
    /// <para>
    /// Models the toolkits return are <see cref="Dictionary{TKey, TValue}"/> and implement
    /// <see cref="IDictionary"/> directly. An object literal written in JavaScript, however, reaches .NET as a
    /// <c>System.Dynamic.ExpandoObject</c>, which implements only <see cref="IDictionary{TKey, TValue}"/>.
    /// A bare <c>as IDictionary</c> therefore yields <see langword="null"/> for every options object an agent
    /// passes — which silently degrades to default styling with no error rather than failing loudly.
    /// </para>
    /// </summary>
    public static IDictionary? AsDict(object? value)
    {
        switch (value)
        {
            case null:
                return null;

            case IDictionary dictionary:
                return dictionary;

            case IDictionary<string, object?> generic:
                var table = new Hashtable(generic.Count);
                foreach (var entry in generic) table[entry.Key] = entry.Value;
                return table;

            default:
                return null;
        }
    }
    #endregion
}
