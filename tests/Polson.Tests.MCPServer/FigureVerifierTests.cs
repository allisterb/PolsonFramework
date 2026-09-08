namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using Polson.ExtendedMind.ParallelSearch;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Checking the drawn figures against the research they claim to rest on.
/// </summary>
/// <remarks>
/// <b>The point is that the agent does not write this verdict.</b> A run's own audit is composed by
/// the agent that drew the piece, so it checks what that agent thought to check — seventeen passing
/// checks on the kubrick5 page, under a headline of <c>27 / 10</c> whose own cards summed to 27 and
/// 9. This reads the saved markup and the archived result and compares them, so nothing in the
/// answer is a matter of opinion.
/// </remarks>
public class FigureVerifierTests : TestsRuntime, IDisposable
{
    #region Constructors
    public FigureVerifierTests()
    {
        root = Path.Combine(Path.GetTempPath(), "polson-verify-" + Guid.NewGuid().ToString("N")[..8]);
        archive = new ResearchArchive(root);

        new ResearchRegistry(2, archive).Complete(
            new ResearchTask { Id = Run, Description = "films", Objective = "runtimes", Processor = "base" },
            new Dictionary<string, object?>
            {
                ["totalRuntimeMinutes"] = 1636,
                ["totalGross"] = 171.9,
                ["films"] = new object[]
                {
                    new Dictionary<string, object?> { ["title"] = "Fear and Desire", ["runtime"] = 62 },
                    new Dictionary<string, object?> { ["title"] = "Spartacus", ["runtime"] = 197 },
                },
            },
            []);
    }
    #endregion

    #region Methods
    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private static string Svg(string body) =>
        "<svg xmlns='http://www.w3.org/2000/svg'>" + body + "</svg>";

    private FigureVerifier.Verdict Check(string body) => FigureVerifier.Verify(Svg(body), archive);
    #endregion

    #region Tests
    /// <summary>A tagged figure that matches its research reconciles.</summary>
    [Fact]
    public void TestATaggedFigureThatMatchesIsVerified()
    {
        var verdict = Check($"<text data-basis='{Run}:totalRuntimeMinutes'>1636</text>");

        Assert.Equal(1, verdict.Checked);
        Assert.Equal(1, verdict.Verified);
        Assert.Empty(verdict.Findings);
    }

    /// <summary>The case this exists for: a number that does not match, caught without the agent.</summary>
    [Fact]
    public void TestAFigureThatDoesNotMatchIsReportedWithBothValues()
    {
        var verdict = Check($"<text data-basis='{Run}:totalRuntimeMinutes'>1836</text>");

        Assert.Equal(0, verdict.Verified);
        var finding = Assert.Single(verdict.Findings);
        Assert.Equal("1836", finding.Drawn);
        Assert.Equal("1636", finding.Expected);
    }

    /// <summary>
    /// Presentation is not disagreement.
    /// </summary>
    /// <remarks>
    /// A check that called <c>$171.9M</c> a mismatch against an archived <c>171.9</c> would be
    /// switched off inside one run, and rightly — every deliverable formats its numbers. Currency,
    /// grouping and a trailing unit are the caller's business.
    /// </remarks>
    [Theory]
    [InlineData("$171.9M")]
    [InlineData("171.9")]
    [InlineData("171.9m")]
    public void TestFormattingIsNotAMismatch(string drawn)
    {
        Assert.Empty(Check($"<text data-basis='{Run}:totalGross'>{drawn}</text>").Findings);
    }

    /// <summary>Grouping separators do not make a number disagree with itself.</summary>
    [Fact]
    public void TestAGroupedNumberStillReconciles()
    {
        Assert.Empty(Check($"<text data-basis='{Run}:totalRuntimeMinutes'>1,636</text>").Findings);
    }

    /// <summary>An array element is addressed the way a citation addresses one.</summary>
    /// <remarks>
    /// <c>basis.field</c> uses a dot index for a list element — <c>missions.0</c> — so a tag naming
    /// <c>films.1.runtime</c> follows the service's own convention rather than inventing a second.
    /// </remarks>
    [Fact]
    public void TestAnArrayElementResolvesByDotIndex()
    {
        Assert.Empty(Check($"<text data-basis='{Run}:films.1.runtime'>197m</text>").Findings);
        Assert.Empty(Check($"<text data-basis='{Run}:films.0.title'>Fear and Desire</text>").Findings);
    }

    /// <summary>A tag naming a field that is not there says so rather than passing.</summary>
    [Fact]
    public void TestAFieldThatDoesNotExistIsAFinding()
    {
        var finding = Assert.Single(Check($"<text data-basis='{Run}:films.9.runtime'>1</text>").Findings);
        Assert.Contains("no field", finding.Reason, StringComparison.Ordinal);
    }

    /// <summary>A tag naming a run nobody archived is unverifiable, and reported as such.</summary>
    /// <remarks>
    /// Distinct from a mismatch on purpose. "I cannot check this" and "this is wrong" call for
    /// different actions, and collapsing them would let a missing archive read as a failed figure.
    /// </remarks>
    [Fact]
    public void TestAnUnarchivedRunIsReportedAsUncheckable()
    {
        var finding = Assert.Single(Check("<text data-basis='trun_missing:x'>1</text>").Findings);
        Assert.Contains("not archived", finding.Reason, StringComparison.Ordinal);
    }

    /// <summary>Untagged text is not examined, so ticks and furniture cost nothing.</summary>
    /// <remarks>
    /// The reason the check is tag-driven rather than a sweep for numerals: a page carries axis
    /// ticks, years and page furniture that are structural, not data. A sweep would flag dozens and
    /// be ignored within a run.
    /// </remarks>
    [Fact]
    public void TestUntaggedTextIsIgnored()
    {
        var verdict = Check("<text>0</text><text>50</text><text>100</text><text>1950</text>");

        Assert.Equal(0, verdict.Checked);
        Assert.Empty(verdict.Findings);
    }

    /// <summary>A figure composed of several spans is read whole.</summary>
    /// <remarks>
    /// Tracked type is one element per glyph and a wrapped paragraph one span per line, so the value
    /// is often not on the tagged node itself. Reading only the node's own text would report a
    /// correct tracked figure as empty.
    /// </remarks>
    [Fact]
    public void TestAFigureSplitAcrossSpansIsReadWhole()
    {
        var verdict = Check(
            $"<g data-basis='{Run}:totalRuntimeMinutes'><text>1</text><text>6</text><text>36</text></g>");

        Assert.Equal(1, verdict.Verified);
    }

    /// <summary>A malformed tag is a finding, not a silent pass.</summary>
    [Fact]
    public void TestATagThatIsNotRunIdAndFieldIsAFinding()
    {
        Assert.Single(Check($"<text data-basis='{Run}'>1636</text>").Findings);
    }

    /// <summary>Markup that will not parse reports that rather than throwing.</summary>
    [Fact]
    public void TestUnparseableMarkupIsAFindingRatherThanACrash()
    {
        var verdict = FigureVerifier.Verify("<svg><text>unclosed", archive);

        Assert.Single(verdict.Findings);
        Assert.Equal(0, verdict.Verified);
    }
    #endregion

    #region Fields
    private const string Run = "trun_verify1";

    private readonly string root;
    private readonly ResearchArchive archive;
    #endregion
}
