namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Polson.Drawing.Svg;
using Svg;
using Xunit;

/// <summary>
/// ApplyAttribute and GetAttribute are one property with two halves: whatever attr(name, value)
/// accepts, attr(name) must give back. They drifted apart — roughly a dozen attributes could be set
/// and always read as null, which to a script is indistinguishable from "the set did not take", and
/// is what a timeline restoring a value it saved would silently get wrong.
/// The theory below fixes the instance; TestEverySetterCaseHasAGetterArm fixes the class.
/// </summary>
public class SnapAttributeRoundTripTests : TestsRuntime
{
    #region Round-Trip Tests
    /// <summary>
    /// Set it, read it, and check the read-back is the SVG spelling — 'middle', never 'Middle'.
    /// Each row then re-applies what it read and asserts the value does not move, which is the part
    /// that matters: a getter is only useful if its output is something the setter still accepts.
    /// </summary>
    [Theory]
    // Previously write-only: enums, formatted back to the spelling their own parser takes.
    [InlineData("text", "font-weight", "bold", "bold")]
    [InlineData("text", "font-weight", "bolder", "bolder")]
    [InlineData("text", "font-weight", "lighter", "lighter")]
    [InlineData("text", "font-weight", "normal", "normal")]
    [InlineData("text", "font-weight", "700", "700")]
    [InlineData("text", "font-weight", "100", "100")]
    [InlineData("text", "font-weight", "900", "900")]
    [InlineData("text", "fontWeight", "bold", "bold")]
    [InlineData("text", "text-anchor", "start", "start")]
    [InlineData("text", "text-anchor", "middle", "middle")]
    [InlineData("text", "text-anchor", "end", "end")]
    [InlineData("text", "textAnchor", "middle", "middle")]
    // The setter takes two extra spellings; both must normalise to what SVG calls them.
    [InlineData("text", "text-anchor", "center", "middle")]
    [InlineData("text", "text-anchor", "right", "end")]
    [InlineData("rect", "stroke-linecap", "butt", "butt")]
    [InlineData("rect", "stroke-linecap", "round", "round")]
    [InlineData("rect", "stroke-linecap", "square", "square")]
    [InlineData("rect", "strokeLineCap", "round", "round")]
    [InlineData("rect", "stroke-linejoin", "miter", "miter")]
    [InlineData("rect", "stroke-linejoin", "round", "round")]
    [InlineData("rect", "stroke-linejoin", "bevel", "bevel")]
    [InlineData("rect", "strokeLineJoin", "bevel", "bevel")]
    // Previously write-only: units and collections.
    [InlineData("text", "font-size", "18", "18")]
    [InlineData("text", "fontSize", "18", "18")]
    [InlineData("text", "font-size", "13.5", "13.5")]
    [InlineData("rect", "stroke-dashoffset", "3", "3")]
    [InlineData("rect", "strokeDashOffset", "3", "3")]
    [InlineData("rect", "stroke-dasharray", "4 2", "4 2")]
    [InlineData("rect", "strokeDashArray", "4 2", "4 2")]
    // A comma-separated dash pattern is accepted and comes back space-separated, which the setter
    // also takes — so the value is stable even though the spelling was normalised.
    [InlineData("rect", "stroke-dasharray", "10,5,2,5", "10 5 2 5")]
    [InlineData("polyline", "points", "10,20 30,40", "10,20 30,40")]
    [InlineData("polygon", "points", "0,0 10,0 10,10", "0,0 10,0 10,10")]
    [InlineData("polygon", "points", "0 0 10 0 10 10", "0,0 10,0 10,10")]
    [InlineData("svg", "viewBox", "0 0 100 50", "0 0 100 50")]
    [InlineData("svg", "viewbox", "-10 -5 100 50", "-10 -5 100 50")]
    [InlineData("svg", "viewBox", "0,0,320,240", "0 0 320 240")]
    // Previously write-only: the three href spellings, on both elements the setter writes to.
    [InlineData("image", "href", "artifacts/tile.png", "artifacts/tile.png")]
    [InlineData("image", "src", "artifacts/tile.png", "artifacts/tile.png")]
    [InlineData("image", "xlink:href", "artifacts/tile.png", "artifacts/tile.png")]
    [InlineData("use", "href", "#mark", "#mark")]
    // The setter adds the '#' a bare id is missing; the getter must report what was actually stored.
    [InlineData("use", "href", "mark", "#mark")]
    // Previously write-only: a line's endpoints, and the gradient coordinates sharing those setters.
    [InlineData("line", "x1", "10", "10")]
    [InlineData("line", "y1", "20", "20")]
    [InlineData("line", "x2", "30", "30")]
    [InlineData("line", "y2", "40", "40")]
    [InlineData("linearGradient", "x1", "0.25", "0.25")]
    [InlineData("linearGradient", "y1", "0", "0")]
    [InlineData("linearGradient", "x2", "0.75", "0.75")]
    [InlineData("linearGradient", "y2", "1", "1")]
    [InlineData("radialGradient", "cx", "0.5", "0.5")]
    [InlineData("radialGradient", "cy", "0.5", "0.5")]
    [InlineData("radialGradient", "r", "0.5", "0.5")]
    // Already round-tripping, kept so a regression in the shared plumbing is caught here too.
    [InlineData("rect", "id", "mark", "mark")]
    [InlineData("rect", "class", "guide", "guide")]
    [InlineData("rect", "stroke-width", "2.5", "2.5")]
    [InlineData("rect", "opacity", "0.4", "0.4")]
    [InlineData("rect", "fill", "#ff0000", "#FF0000")]
    [InlineData("rect", "x", "12", "12")]
    [InlineData("rect", "width", "80", "80")]
    [InlineData("circle", "r", "9", "9")]
    [InlineData("text", "font-family", "Georgia", "Georgia")]
    [InlineData("rect", "display", "none", "none")]
    [InlineData("rect", "visibility", "hidden", "hidden")]
    // Path data comes back through Svg.NET's own serializer, which writes spaces where the input had
    // commas. Still a round trip — the re-apply below proves the read-back parses to the same path.
    [InlineData("path", "d", "M10,10 L20,20", "M10 10 L20 20")]
    // An attribute the switch does not know falls through to CustomAttributes, both ways.
    [InlineData("rect", "data-role", "backdrop", "backdrop")]
    public void TestAttributeRoundTrips(string kind, string attribute, string input, string expected)
    {
        var element = CreateElement(kind);

        SnapAttributes.ApplyAttribute(element, attribute, input);
        var readBack = Format(SnapAttributes.GetAttribute(element, attribute));
        Assert.Equal(expected, readBack);

        // The read-back has to be usable as an input, or it is not a round trip — only a report.
        SnapAttributes.ApplyAttribute(element, attribute, readBack);
        Assert.Equal(expected, Format(SnapAttributes.GetAttribute(element, attribute)));
    }

    /// <summary>
    /// The same property through the JS-facing surface, since that is where a script meets it and
    /// where the bug was reported: el.attr(name, value) then el.attr(name).
    /// </summary>
    [Fact]
    public void TestRoundTripThroughSnapElement()
    {
        var paper = new SnapPaper(200, 100);
        var text = paper.Text(10, 20, "Polson");

        text.Attr("text-anchor", "middle").Attr("font-weight", "700").Attr("font-size", "24");
        Assert.Equal("middle", text.Attr("text-anchor"));
        Assert.Equal("700", text.Attr("font-weight"));
        Assert.Equal(24f, text.Attr("font-size"));

        var line = paper.Line(0, 0, 0, 0);
        line.Attr("x1", "5").Attr("y1", "6").Attr("x2", "7").Attr("y2", "8");
        Assert.Equal(5f, line.Attr("x1"));
        Assert.Equal(6f, line.Attr("y1"));
        Assert.Equal(7f, line.Attr("x2"));
        Assert.Equal(8f, line.Attr("y2"));
    }

    /// <summary>
    /// A null read must keep meaning "this element has no such attribute". The fix must not turn an
    /// honest absence into a fabricated default on elements the setter would not have written to.
    /// </summary>
    [Fact]
    public void TestUnsetAndInapplicableAttributesStillReadNull()
    {
        var rect = new SvgRectangle();
        Assert.Null(SnapAttributes.GetAttribute(rect, "points"));
        Assert.Null(SnapAttributes.GetAttribute(rect, "viewBox"));
        Assert.Null(SnapAttributes.GetAttribute(rect, "href"));
        Assert.Null(SnapAttributes.GetAttribute(rect, "x1"));
        Assert.Null(SnapAttributes.GetAttribute(rect, "stroke-dasharray"));

        Assert.Null(SnapAttributes.GetAttribute(new SvgFragment(), "viewBox"));
        Assert.Null(SnapAttributes.GetAttribute(new SvgPolyline(), "points"));
        Assert.Null(SnapAttributes.GetAttribute(new SvgImage(), "href"));
    }

    /// <summary>
    /// Every enum value the parsers can produce must have a spelling, and that spelling must parse
    /// back to the same value — otherwise a getter fixed for the common cases still lies at the edge.
    /// </summary>
    [Theory]
    [InlineData("stroke-linecap", "butt", "round", "square")]
    [InlineData("stroke-linejoin", "miter", "round", "bevel")]
    [InlineData("text-anchor", "start", "middle", "end")]
    public void TestEnumSpellingsAreLowerCaseSvgNames(string attribute, string a, string b, string c)
    {
        foreach (var spelling in new[] { a, b, c })
        {
            var element = new SvgText();
            SnapAttributes.ApplyAttribute(element, attribute, spelling);
            var readBack = SnapAttributes.GetAttribute(element, attribute) as string;
            Assert.Equal(spelling, readBack);
            Assert.NotNull(readBack);
            Assert.Equal(readBack, readBack.ToLowerInvariant());
        }
    }
    #endregion

    #region Setter/Getter Parity Tests
    /// <summary>
    /// The class of bug, rather than this instance of it. Reads the two switches out of
    /// SnapAttributes.cs and asserts they cover the same attribute names, so the next attribute
    /// added to the setter without a getter arm fails here instead of reaching a script as a null.
    /// </summary>
    [Fact]
    public void TestEverySetterCaseHasAGetterArm()
    {
        var source = ReadSnapAttributesSource();
        var setterKeys = SetterCaseLabels(source);
        var getterKeys = GetterArmLabels(source);

        // A regex that matched nothing would let this pass while asserting nothing at all.
        Assert.True(setterKeys.Count > 30, $"only {setterKeys.Count} setter case labels found — the scan is broken, not the switch");
        Assert.True(getterKeys.Count > 30, $"only {getterKeys.Count} getter arm labels found — the scan is broken, not the switch");

        var missing = setterKeys.Except(getterKeys).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0,
            "settable but not readable through attr(): " + string.Join(", ", missing) +
            " — add an arm to GetAttribute, or attr() will answer null for a value that was set");
    }

    /// <summary>
    /// The other direction, which catches the subtler drift: a getter arm for a name the setter
    /// routes to CustomAttributes reads from the typed property while the write went to the bag.
    /// </summary>
    [Fact]
    public void TestEveryGetterArmHasASetterCase()
    {
        var source = ReadSnapAttributesSource();
        var setterKeys = SetterCaseLabels(source);
        var getterKeys = GetterArmLabels(source);

        var orphaned = getterKeys.Except(setterKeys).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(orphaned.Count == 0,
            "readable but not settable through attr(): " + string.Join(", ", orphaned));
    }

    /// <summary>
    /// NormalizeKey lower-cases, so a case label carrying a capital can never be reached. One such
    /// label ("viewBox") sat in the setter unread; this keeps the next one from being written.
    /// </summary>
    [Fact]
    public void TestNoCaseLabelIsUnreachableThroughNormalizeKey()
    {
        var source = ReadSnapAttributesSource();
        var unreachable = SetterCaseLabels(source)
            .Concat(GetterArmLabels(source))
            .Where(k => k != k.Replace("_", string.Empty).ToLowerInvariant())
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(unreachable.Count == 0,
            "case labels NormalizeKey can never produce: " + string.Join(", ", unreachable));
    }
    #endregion

    #region Helper Methods
    private static SvgElement CreateElement(string kind) =>
        kind switch
        {
            "rect" => new SvgRectangle(),
            "circle" => new SvgCircle(),
            "ellipse" => new SvgEllipse(),
            "line" => new SvgLine(),
            "path" => new SvgPath(),
            "text" => new SvgText(),
            "image" => new SvgImage(),
            "use" => new SvgUse(),
            "polyline" => new SvgPolyline(),
            "polygon" => new SvgPolygon(),
            "svg" => new SvgFragment(),
            "linearGradient" => new SvgLinearGradientServer(),
            "radialGradient" => new SvgRadialGradientServer(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown element kind")
        };

    private static string? Format(object? value) =>
        value switch
        {
            null => null,
            float f => f.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

    /// <summary>
    /// Located from this file's own compile-time path, so the scan does not depend on the working
    /// directory or on anything having been copied to the output.
    /// </summary>
    private static string ReadSnapAttributesSource([CallerFilePath] string thisFile = "")
    {
        var testsDir = Path.GetDirectoryName(thisFile)!;
        var path = Path.GetFullPath(Path.Combine(testsDir, "..", "..", "src", "Polson.Drawing.Svg", "SnapAttributes.cs"));
        Assert.True(File.Exists(path), $"cannot read the source under test at {path}");
        return File.ReadAllText(path);
    }

    private static string Region(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"marker not found in source: {startMarker}");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"marker not found in source: {endMarker}");
        return source[start..end];
    }

    private static IEnumerable<string> CodeLines(string region) =>
        region.Split('\n').Select(l => l.Trim()).Where(l => !l.StartsWith("//", StringComparison.Ordinal));

    private static HashSet<string> SetterCaseLabels(string source)
    {
        var region = Region(source, "public static void ApplyAttribute(", "public static void ApplyAttributes(");
        return CodeLines(region)
            .SelectMany(l => Regex.Matches(l, "case\\s+\"([^\"]*)\"\\s*:").Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> GetterArmLabels(string source)
    {
        var region = Region(source, "public static object? GetAttribute(", "public static SvgPaintServer ParsePaintServer(");
        var labels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in CodeLines(region))
        {
            // Only the pattern side of an arm names an attribute; the right-hand side may quote
            // anything, and does — the CustomAttributes arm reads a "class" key out of the bag.
            var arrow = line.IndexOf("=>", StringComparison.Ordinal);
            if (arrow < 0) continue;
            foreach (Match m in Regex.Matches(line[..arrow], "\"([^\"]*)\""))
            {
                labels.Add(m.Groups[1].Value);
            }
        }
        return labels;
    }
    #endregion
}
