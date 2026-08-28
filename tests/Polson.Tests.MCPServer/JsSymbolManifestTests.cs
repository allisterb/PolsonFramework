namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Polson.MCPServer;

/// <summary>
/// Covers the machine-readable symbol manifest.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ApiDocumentationTests"/> keeps the reference honest against reflection. This keeps the
/// manifest honest against the <i>engine</i>, which is a different failure: a global can be
/// registered in <see cref="JsDrawingEngine"/> with no receiver declared for it, and then every
/// member hanging off that global is missing from the index without anything going red.
/// </para>
/// <para>
/// That is not hypothetical. The <c>VectorLogo</c> area was absent from both the search corpus and
/// its own resource URI because a heading-text mismatch made the slice return null — and because
/// similarity search always returns its nearest neighbour, queries for vector calls answered
/// confidently with the raster toolkit instead of admitting the gap.
/// </para>
/// </remarks>
public class JsSymbolManifestTests
{
    #region Registration drift
    [Fact]
    public void TestEveryRuntimeGlobalIsDeclared()
    {
        var declared = JsSurface.Receivers
            .Select(r => r.Name.Split('.')[0])
            .Concat(JsSurface.FunctionGlobals)
            .ToHashSet(StringComparer.Ordinal);

        var undeclared = JsSymbolManifest.RuntimeGlobals()
            .Where(g => !declared.Contains(g))
            .ToArray();

        Assert.True(undeclared.Length == 0,
            "JsDrawingEngine registers globals that JsSurface does not describe, so their members are "
            + "absent from the symbol manifest and invisible to an agent:\n  "
            + string.Join("\n  ", undeclared)
            + "\nAdd a JsReceiver for each, or list it in JsSurface.FunctionGlobals if it is a bare function.");
    }

    [Fact]
    public void TestEveryDeclaredReceiverIsActuallyRegistered()
    {
        var globals = JsSymbolManifest.RuntimeGlobals().ToHashSet(StringComparer.Ordinal);

        var phantom = JsSurface.Receivers
            .Where(r => !r.IsInstance)
            .Select(r => r.Name.Split('.')[0])
            .Distinct()
            .Where(root => !globals.Contains(root))
            .ToArray();

        Assert.True(phantom.Length == 0,
            "JsSurface declares namespace receivers the engine never registers: " + string.Join(", ", phantom));
    }
    #endregion

    #region Manifest content
    [Fact]
    public void TestManifestCoversEveryArea()
    {
        var areas = JsSymbolManifest.Symbols.Select(s => s.Area).Distinct().ToHashSet(StringComparer.Ordinal);

        foreach (var expected in new[] { "Snap", "Canvas2D", "Skia", "Drawing", "Logo", "VectorLogo", "LogoType", "Globals" })
        {
            Assert.Contains(expected, areas);
        }
    }

    [Fact]
    public void TestEverySymbolHasASignature() => Assert.Empty(
        JsSymbolManifest.Symbols.Where(s => string.IsNullOrWhiteSpace(s.Signature)).Select(s => s.Name));

    [Fact]
    public void TestNoDuplicateNames()
    {
        var dupes = JsSymbolManifest.Symbols
            .GroupBy(s => s.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.True(dupes.Length == 0, "duplicate symbol names: " + string.Join(", ", dupes));
    }

    [Fact]
    public void TestNamespaceAccessorsKeepTheirLeadingCapital()
    {
        // `Skia.Shader` is how the reference spells it and how a script writes it. Lower-casing the
        // first letter the way a method name is lower-cased would make the manifest disagree with both.
        Assert.NotNull(JsSymbolManifest.Resolve("Skia.Shader"));
        Assert.NotNull(JsSymbolManifest.Resolve("Skia.ImageFilter"));
    }
    #endregion

    #region Resolution
    [Fact]
    public void TestResolveFindsARealCall()
    {
        var symbol = JsSymbolManifest.Resolve("paper.circle");

        Assert.NotNull(symbol);
        Assert.Equal("Snap", symbol!.Area);
        Assert.Contains("cx", symbol.Signature);
    }

    [Fact]
    public void TestResolveReturnsANullThatMeansSomething()
    {
        // The whole reason this index exists: a similarity search cannot say "that call does not
        // exist", it can only return its nearest neighbour with a plausible score.
        Assert.Null(JsSymbolManifest.Resolve("paper.squirkle"));
        Assert.Null(JsSymbolManifest.Resolve("ctx.drawEverything"));
        Assert.Null(JsSymbolManifest.Resolve("Snap.path.ogeeCurve2"));
    }

    [Fact]
    public void TestNearestOffersUsableAlternatives()
    {
        var nearest = JsSymbolManifest.Nearest("paper.squirkle", 5).Select(s => s.Name).ToArray();

        Assert.Contains("paper.squircle", nearest);
    }

    [Theory]
    [InlineData("paper.squircle")]
    [InlineData("paper.goldenSpiral")]
    [InlineData("paper.emblemBadge")]
    [InlineData("VectorLogo.squircle")]
    [InlineData("Snap.path.squircle")]
    public void TestVectorLogoCallsResolve(string name)
    {
        // Regression: this whole area sliced to null and was absent from the corpus and its own URI.
        // Anything that hides an area again should fail here rather than mislead an agent at runtime.
        Assert.NotNull(JsSymbolManifest.Resolve(name));
    }

    [Fact]
    public void TestOnReceiverEnumeratesRatherThanSearches()
    {
        var members = JsSymbolManifest.OnReceiver("matrix");

        Assert.NotEmpty(members);
        Assert.All(members, m => Assert.Equal("matrix", m.Receiver));
        Assert.Contains(members, m => m.Member == "rotate");
        Assert.Contains(members, m => m.Member == "invert");
    }

    [Fact]
    public void TestInheritedMembersAreMarked()
    {
        // A paper can take `attr`, but it is documented on `element`. The manifest must still resolve
        // it — an agent that writes paper.attr(...) is correct — while the drift test must not demand
        // that the reference repeat it under both receivers.
        var attr = JsSymbolManifest.Resolve("paper.attr");

        Assert.NotNull(attr);
        Assert.True(attr!.Inherited);
        Assert.False(JsSymbolManifest.Resolve("paper.circle")!.Inherited);
    }
    #endregion

    #region Serialisation
    [Fact]
    public void TestJsonIsWellFormedAndComplete()
    {
        using var document = JsonDocument.Parse(JsSymbolManifest.ToJson());
        var root = document.RootElement;

        Assert.Equal("polson://sdk/symbols", root.GetProperty("schema").GetString());
        Assert.Equal(JsSymbolManifest.Symbols.Count, root.GetProperty("count").GetInt32());

        var first = root.GetProperty("symbols").EnumerateArray().First();
        foreach (var field in new[] { "name", "receiver", "member", "kind", "area", "uri", "signature", "clrType", "clrMember" })
        {
            Assert.False(string.IsNullOrWhiteSpace(first.GetProperty(field).GetString()), field);
        }
    }
    #endregion
}
