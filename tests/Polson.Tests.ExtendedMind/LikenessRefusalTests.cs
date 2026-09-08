namespace Polson.Tests.ExtendedMind;

using System;
using System.Threading.Tasks;
using Polson.ExtendedMind.ImageGeneration;
using Xunit;

/// <summary>
/// A matte will not generate the likeness of a named person.
/// </summary>
/// <remarks>
/// <b>This is the rung that was missing, not a rule that was missing.</b> That a matte is the wrong
/// route to a real person is already written in <c>polson://sdk/core/Assets</c> and in the vector
/// infographic instructions. A live run read "a stencil of the author" in its brief and went round
/// both, three times, until a face came back — and the finished piece carried an invented one under
/// the heading "STANLEY KUBRICK (1928 — 1999)", dated, captioned and with a source line beneath it.
/// Every other integrity check on that page passed, because a wrong face renders perfectly.
/// <para>
/// The form-versus-substance classifier deliberately does not run for <c>matte</c> — a matte <i>is</i>
/// a silhouette — so nothing refused it. Hence a check of its own.
/// </para>
/// </remarks>
public class LikenessRefusalTests : TestsRuntime
{
    #region Tests
    /// <summary>The two descriptors from the run that actually named him.</summary>
    [Theory]
    [InlineData("Stanley Kubrick bearded director portrait with camera")]
    [InlineData("Stanley Kubrick bearded film director portrait head silhouette")]
    public void TestADescriptorNamingAPersonIsRefused(string descriptor)
    {
        Assert.Equal("Stanley Kubrick", AssetRequisitionToolkit.NamedLikeness(descriptor));
    }

    /// <summary>
    /// The third descriptor is not refused, and that is the deliberate limit of this check.
    /// </summary>
    /// <remarks>
    /// "bearded film director portrait stencil silhouette" names nobody. An anonymous figure is a
    /// legitimate graphic form — it is a section marker, not a claim about a person — and what made
    /// the run's version harmful was the caption placed under it, which no descriptor can reveal.
    /// Refusing this too would block the form to catch a caption, and the guidance covers the rest.
    /// </remarks>
    [Fact]
    public void TestAnAnonymousFigureIsStillAllowed()
    {
        Assert.Null(AssetRequisitionToolkit.NamedLikeness("bearded film director portrait stencil silhouette"));
    }

    /// <summary>A name without a face word is a place, a style or a studio — not a likeness.</summary>
    [Theory]
    [InlineData("Golden Gate Bridge cable silhouette")]
    [InlineData("Art Deco fan motif, high contrast")]
    [InlineData("Ben Day dot screen")]
    [InlineData("Northwind Studios logo mark silhouette")]
    public void TestANameWithoutAFaceWordIsNotRefused(string descriptor)
    {
        Assert.Null(AssetRequisitionToolkit.NamedLikeness(descriptor));
    }

    /// <summary>
    /// Both signals are required, and this is why.
    /// </summary>
    /// <remarks>
    /// Either half alone is common and harmless: design vocabulary is full of capitalised pairs, and
    /// a great many legitimate stencils are of anonymous heads. Refusing on one would make the check
    /// noisy enough to be worked around, which is what happened to the paragraph it replaces.
    /// </remarks>
    [Fact]
    public void TestNeitherSignalAloneIsEnough()
    {
        Assert.Null(AssetRequisitionToolkit.NamedLikeness("Art Deco motif"));       // name, no face
        Assert.Null(AssetRequisitionToolkit.NamedLikeness("a portrait silhouette")); // face, no name
        Assert.NotNull(AssetRequisitionToolkit.NamedLikeness("Grace Hopper portrait"));
    }

    /// <summary>An empty or absent descriptor is not a likeness.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TestNothingIsNotSomebody(string descriptor)
    {
        Assert.Null(AssetRequisitionToolkit.NamedLikeness(descriptor));
    }

    /// <summary>The refusal names the person and points at the call that has the gates.</summary>
    /// <remarks>
    /// A refusal that does not say what to do instead gets reworded around, which is exactly how the
    /// run produced its face. The message has to carry the alternative.
    /// </remarks>
    [Fact]
    public async Task TestTheRefusalSaysWhatToDoInsteadAndSpendsNothing()
    {
        var budget = new AssetBudget(5);
        using var generator = new ImageGenerator("dummy-key-not-used-offline");
        var toolkit = new AssetRequisitionToolkit(
            generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), budget, "Framer");

        var matte = await toolkit.Matte("Stanley Kubrick bearded film director portrait head silhouette");

        Assert.False(matte.Success);
        Assert.Equal(ImageGenerationFailure.RefusedLikeness, matte.Failure);

        // A refusal that does not name the alternative gets reworded around — which is exactly how
        // the run produced its face, three descriptors deep.
        Assert.Contains("Photo.of('Stanley Kubrick')", matte.Error, StringComparison.Ordinal);
        Assert.Contains("drop the name", matte.Error, StringComparison.Ordinal);

        // Refused before the network, like a form request: nothing reached, nothing billed.
        Assert.Equal(0, budget.Spent);
        Assert.Empty(matte.Bytes);
    }
    #endregion
}
