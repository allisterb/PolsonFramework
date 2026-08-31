namespace Polson.MCPServer;

using System;
using Jint;
using Jint.Runtime.Interop;

/// <summary>
/// Lets a script name a .NET enum with the string the docs say it can use.
/// </summary>
/// <remarks>
/// Jint's default converter cannot turn <c>'lowerThird'</c> into <c>QuietRegion.LowerThird</c>, so
/// binding a JS options object onto a record with an enum property failed with
/// <c>Invalid cast from 'System.String' to '…QuietRegion'</c> — naming a type the script author has
/// never heard of, about an argument the SDK reference documents as a string.
/// <para>
/// Found by a live painting run: <c>Assets.backdrop(desc, { keepQuiet: 'lowerThird' })</c> is
/// documented exactly that way in <c>docs/Polson.core.md</c> and could not be called at all. The
/// agent worked around it by dropping the argument, which is the option the workflow most wanted.
/// </para>
/// <para>
/// Case-insensitive because the docs use camelCase (<c>'lowerThird'</c>) and the enum is PascalCase.
/// Only strings are handled; everything else falls through to the default converter, so this widens
/// what is accepted and changes nothing that already worked.
/// </para>
/// </remarks>
internal sealed class EnumStringTypeConverter(Engine engine) : DefaultTypeConverter(engine)
{
    #region Methods
    /// <summary>Both entry points are overridden: Jint calls <c>Convert</c> directly in some paths
    /// and <c>TryConvert</c> in others, and overriding only the second silently did nothing.</summary>
    public override object? Convert(object? value, Type type, IFormatProvider formatProvider) =>
        AsEnum(value, type, out var parsed) ? parsed : base.Convert(value, type, formatProvider);

    public override bool TryConvert(object? value, Type type, IFormatProvider formatProvider, out object? converted)
    {
        if (AsEnum(value, type, out var parsed))
        {
            converted = parsed;
            return true;
        }

        return base.TryConvert(value, type, formatProvider, out converted);
    }
    #endregion

    #region Methods (private)
    /// <summary>The enum member a string names, if the target is an enum and the name is a member.</summary>
    /// <remarks>
    /// A blank string is deliberately not "the first member" — it falls through to the default and
    /// fails loudly, as does a name that is not a member at all. Guessing there would be worse than
    /// the crash this replaces: a plate quietly measured against a region nobody asked for.
    /// </remarks>
    static bool AsEnum(object? value, Type type, out object? parsed)
    {
        parsed = null;
        if (value is not string name || string.IsNullOrWhiteSpace(name)) return false;

        var target = Nullable.GetUnderlyingType(type) ?? type;
        return target.IsEnum && Enum.TryParse(target, name, ignoreCase: true, out parsed);
    }
    #endregion

}
