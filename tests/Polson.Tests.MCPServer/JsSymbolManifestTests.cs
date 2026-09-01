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

    /// <summary>
    /// ...and nothing else does. A capital is reserved for a namespace the registry names in its own
    /// right; every other member takes the ordinary camelCase mapping.
    /// </summary>
    /// <remarks>
    /// The rule used to be "any property whose type is a receiver", which asks a different question
    /// and answered it wrongly for every property that merely <i>returns</i> one. The manifest
    /// published <c>element.Paper</c>, <c>paper.Defs</c>, <c>canvas.Bitmap</c>, <c>Snap.Path</c> and
    /// <c>Assets.Budget</c> — five names the reference does not document and no script would type,
    /// offered by <c>Search</c> under a promise that a direct hit is authoritative.
    /// </remarks>
    [Fact]
    public void TestOnlyRegisteredNamespacesAreCapitalised()
    {
        var capitalised = JsSymbolManifest.Symbols
            .Where(s => char.IsUpper(s.Name.Split('.').Last()[0]))
            .Select(s => s.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var registered = JsSurface.Receivers.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        var unexpected = capitalised.Where(n => !registered.Contains(n)).ToArray();

        Assert.True(unexpected.Length == 0,
            "these are published with a leading capital but are not registered namespaces, so the "
            + "manifest is offering a spelling the reference does not use: " + string.Join(", ", unexpected));
    }

    /// <summary>The specific names that were wrong, so the regression is named rather than implied.</summary>
    [Theory]
    [InlineData("Snap.path")]
    [InlineData("paper.defs")]
    [InlineData("element.paper")]
    [InlineData("element.parent")]
    [InlineData("element.children")]
    [InlineData("canvas.bitmap")]
    [InlineData("Assets.budget")]
    public void TestAValueReturningPropertyIsCamelCase(string name)
    {
        Assert.NotNull(JsSymbolManifest.Resolve(name));

        var wrongCase = name[..(name.LastIndexOf('.') + 1)]
            + char.ToUpperInvariant(name.Split('.').Last()[0]) + name.Split('.').Last()[1..];
        Assert.DoesNotContain(JsSymbolManifest.Symbols, s => string.Equals(s.Name, wrongCase, StringComparison.Ordinal));
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

        // `gradient` rather than `circle`, which used to serve here. SnapPaper carried its own copies
        // of the shape factories, character-identical to the inherited ones because a paper's `Node`
        // is its `Document`; deleting them made `paper.circle` genuinely inherited, and the manifest
        // now says so. Paint servers live on the paper alone and are declared there.
        Assert.False(JsSymbolManifest.Resolve("paper.gradient")!.Inherited);
    }

    /// <summary>
    /// The shape factories still resolve on a paper after being left to inheritance.
    /// </summary>
    /// <remarks>
    /// They are the primary way to draw, and an agent writing <c>paper.rect(...)</c> must find them
    /// whichever class declares them. Marked inherited is fine; missing would not be.
    /// </remarks>
    [Theory]
    [InlineData("paper.rect")]
    [InlineData("paper.circle")]
    [InlineData("paper.text")]
    [InlineData("paper.group")]
    public void TestPaperShapeFactoriesStillResolve(string symbol)
    {
        Assert.NotNull(JsSymbolManifest.Resolve(symbol));
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
