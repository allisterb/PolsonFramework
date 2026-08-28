namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Keeps <c>docs/Polson.core.md</c> honest against the code it describes, in both directions.
/// <para>
/// The manual-binding test compares manual citations to the SDK reference — docs against docs — so it
/// cannot see a call the reference documents but no type implements, nor a capability the code has and
/// the reference omits. An agent harness run found one of each: it followed the reference to
/// <c>Snap.path.ogeeCurve</c>, which did not exist, and spent a third of its session working around
/// missing vector gradients that were implemented all along but undocumented. Both classes are checked
/// here against reflection.
/// </para>
/// </summary>
public class ApiDocumentationTests : TestsRuntime
{
    #region Properties
    /// <summary>JS receiver name paired with the .NET type it resolves to.</summary>
    public static TheoryData<string> SurfaceNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in Surfaces.Keys) data.Add(name);
            return data;
        }
    }
    #endregion

    #region Documented-Then-Missing Tests
    [Fact]
    public void TestEveryDocumentedCallExists()
    {
        var missing = new List<string>();

        foreach (Match match in Citation.Matches(Core))
        {
            var receiver = match.Groups[1].Value;
            var member = match.Groups[2].Value;
            if (!Surfaces.TryGetValue(receiver, out var types)) continue;
            if (!types.Any(t => HasMember(t, member)))
            {
                missing.Add($"{receiver}.{member} (not on {string.Join(" or ", types.Select(t => t.Name))})");
            }
        }

        Assert.True(missing.Count == 0,
            "docs/Polson.core.md documents calls that do not exist:\n  " + string.Join("\n  ", missing.Distinct()));
    }
    #endregion

    #region Implemented-Then-Undocumented Tests
    [Theory]
    [MemberData(nameof(SurfaceNames))]
    public void TestEveryPublicMemberIsDocumented(string receiver)
    {
        var types = Surfaces[receiver];
        var undocumented = types.SelectMany(PublicJsMembers)
            .Where(member => !IsDeliberatelyUndocumented(receiver, member))
            // Case-insensitive: Jint resolves the leading-capital spelling too, so `Snap.Create`
            // in the reference and `snap.create` from reflection are the same call.
            .Where(member => !Core.Contains($"{receiver}.{member}", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(m => m, StringComparer.Ordinal)
            .ToArray();

        Assert.True(undocumented.Length == 0,
            $"{string.Join(" / ", types.Select(t => t.Name))} exposes members to scripts that docs/Polson.core.md never mentions, so an agent " +
            $"cannot discover them:\n  {string.Join("\n  ", undocumented.Select(m => $"{receiver}.{m}"))}\n" +
            "Document them, or add them to Undocumented in this test with a reason.");
    }
    #endregion

    #region Methods
    private static string Core { get; } = PolsonResources.Docs.Core();

    private static readonly Regex Citation = new(
        @"\b(Drawing|Logo|LogoType|VectorLogo|Snap\.path|paper|ctx|canvas|bitmap|imageData|gradient|matrix|mina|element)\.([a-zA-Z][A-Za-z0-9]*)\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// A receiver may legitimately name more than one type — <c>gradient</c> is a Canvas2D
    /// <see cref="CanvasGradient"/> in the raster docs and a <see cref="SnapGradient"/> in the vector
    /// ones — so a documented call counts as present if any mapped type has it.
    /// </summary>
    private static readonly Dictionary<string, Type[]> Surfaces =
        JsSurface.Receivers.ToDictionary(r => r.Name, r => r.Types, StringComparer.Ordinal);

    /// <summary>
    /// Members a script can reach but that the reference deliberately omits, with the reason. Anything
    /// not listed here and not documented fails the test.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Undocumented => JsSurface.Excluded;

    private static bool IsDeliberatelyUndocumented(string receiver, string member) =>
        Undocumented.ContainsKey($"{receiver}.{member}");

    /// <summary>Public instance members a script can call, as their JS camelCase spelling.</summary>
    private static IEnumerable<string> PublicJsMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(flags))
        {
            if (method.IsSpecialName) continue;                 // property accessors and operators
            if (method.DeclaringType == typeof(object)) continue;
            if (method.Name is "ToString" or "Equals" or "GetHashCode" or "GetType") continue;

            // Shared with the manifest so both agree on what the surface is. Catches compiler
            // artifacts such as a record's <Clone>$, which no script can call.
            if (JsSurface.NotSurface.Contains(method.Name)) continue;
            yield return Camel(method.Name);
        }

        foreach (var property in type.GetProperties(flags))
        {
            yield return Camel(property.Name);
        }
    }

    private static bool HasMember(Type type, string jsName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                                   BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase;

        // Enumerate rather than GetMethod/GetProperty by name: overloads make those throw
        // AmbiguousMatchException, and an overloaded member is exactly as present as any other.
        return type.GetMethods(flags).Any(m => string.Equals(m.Name, jsName, StringComparison.OrdinalIgnoreCase))
            || type.GetProperties(flags).Any(p => string.Equals(p.Name, jsName, StringComparison.OrdinalIgnoreCase));
    }

    private static string Camel(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    #endregion
}
