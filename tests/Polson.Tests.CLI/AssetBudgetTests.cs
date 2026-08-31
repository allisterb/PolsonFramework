namespace Polson.Tests.CLI;

using Polson.CLI;
using Xunit;

/// <summary>
/// The generation ceiling read from <c>Assets:Budget</c>.
/// </summary>
/// <remarks>
/// The case that matters is the unusable value. <c>int.TryParse</c> leaves <c>0</c> on failure and a
/// budget of zero disables requisition entirely, so a typo would present as "asset generation is
/// broken" with nothing saying why — the reason a bad value falls back to the default rather than
/// being taken at face value.
/// </remarks>
public class AssetBudgetTests
{
    #region Methods
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TestAnAbsentSettingUsesTheDefault(string? configured) =>
        Assert.Equal(120, Program.ResolveAssetBudget(configured));

    [Theory]
    [InlineData("1", 1)]
    [InlineData("12", 12)]
    [InlineData("500", 500)]
    [InlineData("  40  ", 40)]
    public void TestAPositiveNumberIsHonoured(string configured, int expected) =>
        Assert.Equal(expected, Program.ResolveAssetBudget(configured));

    /// <summary>Zero and negatives are refused: turning requisition off is done by omitting the key.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("12.5")]
    [InlineData("1e3")]
    public void TestAnUnusableValueFallsBackRatherThanDisablingRequisition(string configured) =>
        Assert.Equal(120, Program.ResolveAssetBudget(configured));
    #endregion
}
