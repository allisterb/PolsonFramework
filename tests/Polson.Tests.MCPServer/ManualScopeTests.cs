namespace Polson.Tests.MCPServer;

using System;
using System.Linq;

using Polson.MCPServer;

using Xunit;

/// <summary>
/// Every manual is served with the note that says what a manual is for.
/// </summary>
/// <remarks>
/// <para>
/// The note explains that a manual teaches a craft once you have chosen it and does not decide that
/// the craft is right for the job — the distinction that keeps an agent from reading a list of chart
/// forms as the list of things a graphic may be.
/// </para>
/// <para>
/// It is tested because of where it lives. The index carried an equivalent paragraph for months and
/// an agent reading <c>polson://manual/06</c> never saw it; that same second-place-copy defect let
/// the index state for two days that no manual cited a book two manuals were written from. Attaching
/// the note to the served body fixes it — and this test is what stops a refactor of
/// <c>ManualResources</c> from quietly detaching it again.
/// </para>
/// </remarks>
public class ManualScopeTests : TestsRuntime
{
    #region Tests
    [Fact]
    public void TestEveryManualIsServedWithTheScopeNote()
    {
        Assert.NotEmpty(PolsonManuals.All);

        foreach (var manual in PolsonManuals.All)
        {
            var served = PolsonManuals.ServedBody(manual);

            Assert.StartsWith(PolsonManuals.ScopeNote, served, StringComparison.Ordinal);
            Assert.Contains(manual.Body, served, StringComparison.Ordinal);
        }
    }

    /// <summary>The note has to actually say the thing, not merely be present.</summary>
    [Fact]
    public void TestTheNoteStatesWhatAManualGoverns()
    {
        var note = PolsonManuals.ScopeNote;

        Assert.Contains("once you have chosen it", note, StringComparison.Ordinal);
        Assert.Contains("recommendations", note, StringComparison.Ordinal);

        // The one exception is named, so "these are recommendations" cannot be read onto it.
        Assert.Contains("Manual 13", note, StringComparison.Ordinal);
        Assert.Contains("truth", note, StringComparison.Ordinal);
    }

    /// <summary>
    /// The body itself stays clean, because three other things read it.
    /// </summary>
    /// <remarks>
    /// <c>ManualExampleTests</c> extracts runnable examples from it, <c>ManualCoverageTests</c>
    /// measures API coverage over it, and the knowledge corpus chunks it for search — twenty-five
    /// identical preamble chunks would be search noise and a preamble in the coverage corpus would
    /// count words nobody wrote.
    /// </remarks>
    [Fact]
    public void TestTheNoteIsAddedAtServeTimeAndNotBakedIntoTheBody()
    {
        foreach (var manual in PolsonManuals.All)
        {
            Assert.DoesNotContain("What a studio manual is", manual.Body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// No manual's Purpose line states one tradition as law.
    /// </summary>
    /// <remarks>
    /// A register check, and deliberately a check on <b>phrases</b> rather than words. Written
    /// against the bare words it failed immediately and correctly-looking: Manual 12's own
    /// disclaimer — *"not a required sequence"* — matched a search for "required", because a
    /// substring cannot tell a claim from its negation. Matching the constructions that actually
    /// occurred keeps the guard useful without forcing the disclaimers to be reworded around it.
    /// <para>
    /// So this catches the register creeping back, not every possible overstatement; it is a lint,
    /// not a proof. Manual 13 is exempt because its §2 genuinely is about truth, which is the one
    /// exception the scope note names.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("Provides exact")]
    [InlineData("rigorous principles")]
    [InlineData("stages required")]
    [InlineData("the rules for")]
    public void TestNoPurposeLineOverstatesItsAuthority(string phrase)
    {
        var overstating = PolsonManuals.All
            .Where(m => m.Id != "13")
            .Where(m => m.Purpose.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            .Select(m => $"{m.Id} — {m.Purpose}")
            .ToArray();

        Assert.True(overstating.Length == 0,
            $"A Purpose line says '{phrase}', which states one tradition as law:\n  "
            + string.Join("\n  ", overstating));
    }
    #endregion
}
