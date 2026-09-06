namespace Polson.Tests.CLI;

using Polson.CLI;
using Xunit;

/// <summary>
/// The photograph ceiling read from <c>Photos:Budget</c>.
/// </summary>
/// <remarks>
/// Nearly the same shape as <see cref="AssetBudgetTests"/> but with one deliberate difference, which
/// is the reason this exists as its own file rather than as two more cases there: <b>zero is a valid
/// setting here</b>. Asset requisition is turned off by leaving the API key unset, which announces
/// itself at startup; reference photography needs no key, so a budget of zero is the only way to
/// switch it off and must be honoured rather than treated as a typo. A negative value is still a typo.
/// </remarks>
public class PhotoBudgetSettingTests
{
    #region Methods
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TestAnAbsentSettingUsesTheDefault(string? configured) =>
        Assert.Equal(24, Program.ResolvePhotoBudget(configured));

    [Theory]
    [InlineData("1", 1)]
    [InlineData("60", 60)]
    [InlineData("  12  ", 12)]
    public void TestAUsableValueIsTaken(string configured, int expected) =>
        Assert.Equal(expected, Program.ResolvePhotoBudget(configured));

    /// <summary>Zero is honoured, because it is the only way to disable a surface that needs no key.</summary>
    [Fact]
    public void TestZeroDisablesRatherThanFallingBack() =>
        Assert.Equal(0, Program.ResolvePhotoBudget("0"));

    [Theory]
    [InlineData("-1")]
    [InlineData("many")]
    [InlineData("3.5")]
    public void TestAnUnusableValueFallsBackToTheDefault(string configured) =>
        Assert.Equal(24, Program.ResolvePhotoBudget(configured));
    #endregion
}
