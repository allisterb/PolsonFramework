namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Svg;
using Svg;
using Xunit;

/// <summary>
/// The general guard: <b>if an element has a property, <c>attr()</c> must reach it.</b>
/// </summary>
/// <remarks>
/// <para>
/// Written after a day of finding the same defect by hand, seven times. The attribute setters are
/// <c>if/else if</c> chains over concrete SVG types, and an element the chain does not name simply
/// falls off the end. Nothing reports it: <c>attr()</c>'s contract turns an unrecognised <i>name</i>
/// into a custom attribute, so a missed <i>type</i> looks exactly like a value that was accepted.
/// </para>
/// <para>
/// Every one of these was live and silent — <c>text</c>/<c>x</c>/<c>y</c>/<c>href</c> on
/// <c>&lt;tspan&gt;</c> and <c>&lt;textPath&gt;</c>, <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c> on
/// <c>&lt;use&gt;</c>, <c>&lt;pattern&gt;</c> and <c>&lt;symbol&gt;</c>, and <c>viewBox</c> on
/// everything but the root. Each was found by reading rather than by any test failing, which is the
/// argument for a guard that does not depend on anyone thinking to look.
/// </para>
/// <para>
/// <b>It enumerates <see cref="SnapPaper.ElementFactories"/> rather than a list of its own</b>, so an
/// element added to the adapter is covered without anyone remembering to cover it. Reflection decides
/// what applies: if the underlying SVG type declares the property, the attribute must round-trip.
/// </para>
/// </remarks>
public class SnapAttributeRoundTripGuardTests : TestsRuntime
{
    #region Properties
    /// <summary>Every element the adapter can create, by its canonical name.</summary>
    public static TheoryData<string> ElementNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in SnapPaper.CanonicalElementNames) data.Add(name);
            return data;
        }
    }
    #endregion

    #region Tests
    /// <summary>
    /// Every attribute an element actually has a property for survives <c>attr()</c> and reads back.
    /// </summary>
    /// <remarks>
    /// The value is compared numerically rather than by string, because the adapter normalises — a
    /// unit comes back as a float and <c>viewBox</c> as four space-separated numbers.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ElementNames))]
    public void TestEveryApplicableAttributeRoundTrips(string tag)
    {
        var element = SnapPaper.CreateElementByName(tag);
        var wrapped = SnapElement.Wrap(element);
        var missed = new List<string>();

        foreach (var (attribute, property, value) in Cases)
        {
            if (!Declares(element, property)) continue;

            wrapped.Attr(attribute, value);
            var read = wrapped.Attr(attribute);

            if (read is null || !Matches(read, value)) missed.Add($"{attribute} (read back {Show(read)})");
        }

        Assert.True(missed.Count == 0,
            $"<{tag}> has the property but attr() did not round-trip: {string.Join(", ", missed)}. "
            + "The setter or getter in SnapAttributes matches a narrower type than the element is.");
    }

    /// <summary>Every registered name creates something, and the error names them all.</summary>
    /// <remarks>
    /// The list in the "not an SVG element" message is now generated from the same table the factory
    /// uses. It used to be a hand-written sentence beside a switch, which is the drift this project has
    /// already been bitten by twice.
    /// </remarks>
    [Fact]
    public void TestTheRefusalMessageListsWhatTheFactoryActuallyHas()
    {
        var error = Assert.Throws<ArgumentException>(() => SnapPaper.CreateElementByName("notAnSvgElement"));

        foreach (var name in SnapPaper.CanonicalElementNames)
        {
            Assert.Contains(name, error.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>An alias creates the same element type as its canonical spelling.</summary>
    [Theory]
    [InlineData("group", "g")]
    [InlineData("text-path", "textPath")]
    [InlineData("clip-path", "clipPath")]
    [InlineData("linear-gradient", "linearGradient")]
    [InlineData("description", "desc")]
    public void TestAnAliasCreatesTheSameType(string alias, string canonical) =>
        Assert.Equal(SnapPaper.CreateElementByName(canonical).GetType(),
                     SnapPaper.CreateElementByName(alias).GetType());
    #endregion

    #region Methods
    /// <summary>Attribute, the CLR property that backs it, and a value to try.</summary>
    /// <remarks>
    /// Deliberately the positional and sizing attributes rather than every attribute there is: they
    /// are the ones shared across unrelated element types, which is exactly where a chain over
    /// concrete types goes wrong. Paint and font attributes are set on <c>SvgElement</c> itself, so
    /// they cannot miss a subtype.
    /// </remarks>
    private static readonly (string Attribute, string Property, object Value)[] Cases =
    [
        ("x", "X", 12f),
        ("y", "Y", 13f),
        ("width", "Width", 34f),
        ("height", "Height", 35f),
        ("cx", "CenterX", 16f),
        ("cy", "CenterY", 17f),
        ("r", "Radius", 18f),
        ("rx", "RadiusX", 19f),
        ("ry", "RadiusY", 20f),
        ("x1", "StartX", 21f),
        ("y1", "StartY", 22f),
        ("x2", "EndX", 23f),
        ("y2", "EndY", 24f),
        ("viewBox", "ViewBox", "0 0 24 25"),
    ];

    /// <summary>Whether the element has that property <b>as a scalar</b>.</summary>
    /// <remarks>
    /// <para>
    /// <b>This guard is about scalar attributes, and says so rather than quietly passing.</b> Some
    /// SVG attributes with the same name are list-valued on some elements: <c>SvgTextBase.X</c> is an
    /// <c>SvgUnitCollection</c> (a per-glyph position list) and <c>SvgMorphology.Radius</c> is an
    /// <c>SvgNumberCollection</c> (<c>rx ry</c>). A scalar setter cannot represent either faithfully,
    /// so comparing one against a single number would fail for a reason that is not the missed-subtype
    /// defect this exists to catch.
    /// </para>
    /// <para>
    /// Both are covered elsewhere by their own shape: spans by <c>SnapTextChildTests</c>, and the
    /// morphology radius by <c>filter.morphology(radius, op)</c>, which sets the collection properly.
    /// </para>
    /// </remarks>
    private static bool Declares(SvgElement element, string property)
    {
        var info = element.GetType().GetProperty(property);
        if (info is null) return false;
        return info.PropertyType == typeof(SvgUnit) || info.PropertyType == typeof(SvgViewBox);
    }

    private static bool Matches(object read, object expected)
    {
        if (expected is float f)
        {
            return float.TryParse(Show(read), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var got)
                   && Math.Abs(got - f) < 0.01f;
        }

        // viewBox: four numbers, however they are punctuated on the way back.
        var wanted = Numbers(expected.ToString()!);
        var actual = Numbers(Show(read));
        return wanted.Length == actual.Length && wanted.Zip(actual).All(p => Math.Abs(p.First - p.Second) < 0.01f);
    }

    private static float[] Numbers(string s) =>
        s.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => float.TryParse(t, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : float.NaN)
            .ToArray();

    private static string Show(object? o) => o?.ToString() ?? "null";
    #endregion
}
