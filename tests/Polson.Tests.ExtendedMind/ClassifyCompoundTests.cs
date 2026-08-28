namespace Polson.Tests.ExtendedMind;

using Polson.ExtendedMind.ImageGeneration;
using Xunit;

/// <summary>
/// The form/substance guard keyed on whole words only, so it fired in both wrong directions for a
/// marine brief: it refused "weathered teak boat decking" (a genuine material, because neither
/// "teak" nor "decking" was known as a substance) while allowing the bare object "a sailboat"
/// (because "boat" is not a whole word inside "sailboat"). Found by the logo harness run.
/// </summary>
public class ClassifyCompoundTests : TestsRuntime
{
    #region Compound Object Tests
    [Theory]
    [InlineData("a sailboat")]
    [InlineData("a sailboat at sunset")]
    [InlineData("a steamship")]
    [InlineData("a lighthouse")]
    [InlineData("a racecar")]
    public void TestCompoundObjectsAreRefused(string descriptor) =>
        Assert.Equal(RequisitionClass.Form, AssetRequisitionToolkit.Classify(descriptor).Class);

    /// <summary>Plurals are the same request.</summary>
    [Theory]
    [InlineData("sailboats")]
    [InlineData("wooden ships")]
    public void TestPluralObjectsAreRefused(string descriptor) =>
        Assert.Equal(RequisitionClass.Form, AssetRequisitionToolkit.Classify(descriptor).Class);
    #endregion

    #region Marine Material Tests
    [Theory]
    [InlineData("weathered teak boat decking")]
    [InlineData("weathered teak decking")]
    [InlineData("boat hull planking, tarred seams")]
    [InlineData("sun-bleached canvas sailcloth weave")]
    [InlineData("coiled hemp rope fibre")]
    [InlineData("varnished mahogany grain")]
    public void TestMarineMaterialsAreAllowed(string descriptor)
    {
        var verdict = AssetRequisitionToolkit.Classify(descriptor);
        Assert.True(verdict.Allowed, $"'{descriptor}' was refused: {verdict.Reason}");
        Assert.Equal(RequisitionClass.Substance, verdict.Class);
    }

    /// <summary>Compound substances must match the same way compound objects do.</summary>
    [Theory]
    [InlineData("rough limestone surface")]
    [InlineData("pitted sandstone")]
    public void TestCompoundMaterialsAreAllowed(string descriptor) =>
        Assert.Equal(RequisitionClass.Substance, AssetRequisitionToolkit.Classify(descriptor).Class);
    #endregion

    #region Suffix False-Friend Tests
    /// <summary>
    /// The compound rule is a suffix test, so words merely ending in a form noun must not trip it.
    /// "fine craftsmanship" is a description of a material's finish, not a request for a ship.
    /// </summary>
    [Theory]
    [InlineData("tooled leather showing fine craftsmanship")]
    [InlineData("oak panelling of careful workmanship")]
    public void TestAbstractShipNounsAreNotObjects(string descriptor)
    {
        var verdict = AssetRequisitionToolkit.Classify(descriptor);
        Assert.True(verdict.Allowed, $"'{descriptor}' was refused: {verdict.Reason}");
    }
    #endregion

    #region Preserved Behaviour Tests
    [Theory]
    [InlineData("weathered wooden ship hull planking", RequisitionClass.Substance)]
    [InlineData("ship hull planking", RequisitionClass.Substance)]
    [InlineData("a wooden ship", RequisitionClass.Form)]
    [InlineData("a ship made of oak", RequisitionClass.Form)]
    [InlineData("the company logo", RequisitionClass.Form)]
    [InlineData("a dragon", RequisitionClass.Form)]
    [InlineData("brushed copper surface", RequisitionClass.Substance)]
    public void TestExistingVerdictsAreUnchanged(string descriptor, RequisitionClass expected) =>
        Assert.Equal(expected, AssetRequisitionToolkit.Classify(descriptor).Class);

    /// <summary>The readable name added for scripts must track the enum.</summary>
    [Fact]
    public void TestClassNameMatchesClass()
    {
        Assert.Equal("Form", AssetRequisitionToolkit.Classify("a sailboat").ClassName);
        Assert.Equal("Substance", AssetRequisitionToolkit.Classify("weathered teak decking").ClassName);
    }
    #endregion
}
