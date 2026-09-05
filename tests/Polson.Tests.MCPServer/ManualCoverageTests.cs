namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// How much of the SDK an agent can reach through the <em>manuals</em> rather than only through the
/// API reference.
/// </summary>
/// <remarks>
/// <see cref="ApiDocumentationTests"/> already keeps <c>docs/Polson.core.md</c> honest against
/// reflection, so the reference is complete by construction. This asks a different question: the
/// reference answers <i>what do I call</i>, and the manuals answer <i>how should this look</i> — and
/// an agent that does not already know a capability exists only finds it through the second.
/// <para>
/// The failure this exists to catch is silent and has happened. A painting run reached its
/// atmosphere stage, was pointed by its workflow at the lighting manual, found no mention of noise
/// there — the atmospheric shader preset is filed in the cel-shading manual — and drew haze as
/// flat ellipses at low alpha. The capability existed, was documented in the reference, and never
/// reached the agent.
/// </para>
/// <para>
/// <b>This is a ratchet, not a target.</b> The floors below are the coverage measured when each was
/// last raised. Improving a manual can only ever make the test pass more comfortably; adding API
/// surface with no manual to reach it through is what makes it fail. When you legitimately improve
/// coverage, raise the floor — that is the whole mechanism, and an unraised floor costs nothing.
/// </para>
/// </remarks>
public class ManualCoverageTests : TestsRuntime
{
    #region Constants
    /// <summary>
    /// Shortest member name compared. Below this, matching is accidental — <c>x</c>, <c>cx</c> and
    /// <c>map</c> appear in English prose and would report themselves as covered everywhere.
    /// </summary>
    private const int MinimumNameLength = 4;

    /// <summary>
    /// Coverage each area must not fall below, as a percentage.
    /// </summary>
    /// <remarks>
    /// Measured 2026-09-01 against the manual corpus as it then stood (17 manuals), under the
    /// <c>.member</c> match
    /// described on <see cref="Measure"/>. They are deliberately set <i>at</i> the measured value
    /// rather than below it: a floor with slack is a floor that lets the first regression through,
    /// which is the one worth catching.
    /// <para>
    /// <b>Not comparable to anything recorded before Manual 14.</b> Those figures came from a bare-word
    /// match, which counted ordinary English: a vector manual that never mentions requisition moved
    /// <c>Assets</c> from 35% to 58% on the words <i>bytes</i>, <i>size</i> and <i>success</i> alone.
    /// Under the dot rule the same corpus put <c>Assets</c> at 17% and <c>Globals</c> at 5%, which was
    /// the truth, and those two gaps were exactly what the old instrument had been hiding. Manuals 15
    /// and 16 then took <c>Assets</c> to 92% and <c>Skia</c> to 67% for real.
    /// </para>
    /// <para>
    /// <c>Drawing</c> sits near 100 because manuals 01–09 are written on that toolkit. It is the shape
    /// the other areas are being measured against, not an aspiration for all of them — a raster
    /// primitive like <c>ctx.miterLimit</c> does not need design theory written about it.
    /// </para>
    /// <para>
    /// <b>Part of what looked like a manual gap was a manifest defect, and was fixed there instead.</b>
    /// <c>Snap</c> rose from 70% to 82% without a word being written: the manifest had been publishing
    /// <c>Snap.Path</c>, <c>element.Paper</c>, <c>paper.Defs</c>, <c>element.Parent</c> and
    /// <c>Assets.Budget</c> capitalised — spellings the reference does not document — and leaking
    /// <c>paper.node</c> / <c>gradient.node</c>, raw escape hatches excluded on <c>element</c> but not
    /// on the receivers that inherit them. A symbol nobody should type is not a gap a manual can close.
    /// </para>
    /// <para>
    /// What remains is thinner and more defensible. <c>Scale</c> (54%) is accessor properties —
    /// <c>band.step</c>, <c>scale.domainStart</c> — that a chart reads without a manual naming them;
    /// <c>Canvas2D</c> (75%) is raster primitives like <c>ctx.miterLimit</c> that need no design theory
    /// written about them; and <c>Snap</c>'s residue is mostly aliases (<c>group</c> for <c>g</c>,
    /// <c>append</c> for <c>add</c>) plus every member <c>SnapGradient</c> inherits from
    /// <c>SnapElement</c>, counted a second time as <c>gradient.polyline</c> and friends.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, int> Floors = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Skia"] = 100,
        ["Globals"] = 100,
        ["Drawing"] = 97,
        ["Assets"] = 94,
        ["Snap"] = 82,
        ["Canvas2D"] = 75,
        ["VectorLogo"] = 74,
        ["Css"] = 66,
        ["Layout"] = 66,
        ["LogoType"] = 66,
        ["Logo"] = 65,
        ["Scale"] = 54,

        // A spike, and deliberately at zero rather than given a manual it has not earned. The
        // authoring model above it is still an open question — whether a timeline lives in
        // JavaScript or in the engine, and whether easings are baked to SMIL for export — so a
        // manual written now would document a decision nobody has made. It is in
        // `docs/Polson.core.md` so it is discoverable, and this floor rises when the shape settles.
        ["Motion"] = 0,
    };

    /// <summary>The whole surface, so a new area cannot be added without anyone noticing.</summary>
    private const int OverallFloor = 82;
    #endregion

    #region Properties
    public static TheoryData<string> Areas
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var area in Floors.Keys) data.Add(area);
            return data;
        }
    }
    #endregion

    #region Methods
    [Theory]
    [MemberData(nameof(Areas))]
    public void TestAnAreaStaysReachableFromTheManuals(string area)
    {
        var (total, covered, missing) = Measure(area);

        Assert.True(total > 0, $"No symbols found for area '{area}'. Has it been renamed or removed?");

        var percent = covered * 100 / total;
        Assert.True(percent >= Floors[area],
            $"'{area}' manual coverage fell to {percent}% ({covered}/{total}); the floor is {Floors[area]}%.\n"
            + "Either the new surface needs a manual passage that names it, or the floor needs lowering "
            + "with a reason. Unmentioned:\n  " + string.Join("\n  ", missing.Take(30)));
    }

    [Fact]
    public void TestTheSurfaceAsAWholeStaysReachable()
    {
        var (total, covered, _) = Measure(area: null);
        var percent = covered * 100 / total;

        Assert.True(percent >= OverallFloor,
            $"Manual coverage of the whole SDK fell to {percent}% ({covered}/{total}); the floor is {OverallFloor}%.");
    }

    /// <summary>
    /// Every area the manifest publishes has a floor, so a new one cannot arrive uncounted.
    /// </summary>
    /// <remarks>
    /// Without this, adding an area is the one way to add a large unreachable surface and see every
    /// existing test stay green.
    /// </remarks>
    [Fact]
    public void TestEveryPublishedAreaIsAccountedFor()
    {
        var published = JsSymbolManifest.Symbols
            .Select(s => string.IsNullOrEmpty(s.Area) ? "Globals" : s.Area)
            .Distinct()
            .ToArray();

        var unaccounted = published.Where(a => !Floors.ContainsKey(a)).ToArray();

        Assert.True(unaccounted.Length == 0,
            "These SDK areas have no manual-coverage floor, so nothing measures whether an agent can "
            + "find them: " + string.Join(", ", unaccounted));
    }
    #endregion

    #region Methods (private)
    /// <summary>
    /// Counts how many of an area's members are named anywhere in the manual corpus.
    /// </summary>
    /// <remarks>
    /// The match requires the member name to be preceded by a <b>dot</b> — <c>.getPointAtLength</c>,
    /// not <c>getPointAtLength</c> — so it counts the call being <i>used</i> rather than the word
    /// appearing. The receiver itself is not matched, deliberately: a manual writes
    /// <c>ctx.drawCastShadow(...)</c> for what the manifest calls <c>Drawing.drawCastShadow</c>, and
    /// both spellings are real, so demanding the manifest's receiver would report a covered call as
    /// missing.
    /// <para>
    /// This replaced a bare word-boundary search, which counted English. Under it, "the run reports
    /// success" covered <c>plate.success</c>, "3030 bytes" covered <c>material.bytes</c>, and a manual
    /// about vector geometry raised <c>Assets</c> by 23 points without naming requisition once. The
    /// figures are lower now and they mean something.
    /// </para>
    /// <para>
    /// Still an upper bound, and still generous in one direction: a call named in a "do not use this"
    /// passage counts the same as one taught. The point remains the trend.
    /// </para>
    /// </remarks>
    private static (int Total, int Covered, IReadOnlyList<string> Missing) Measure(string? area)
    {
        var corpus = string.Join("\n", PolsonManuals.All.Select(m => m.Body));
        var total = 0;
        var covered = 0;
        var missing = new List<string>();

        foreach (var symbol in JsSymbolManifest.Symbols.DistinctBy(s => s.Name))
        {
            var symbolArea = string.IsNullOrEmpty(symbol.Area) ? "Globals" : symbol.Area;
            if (area is not null && !string.Equals(symbolArea, area, StringComparison.Ordinal)) continue;

            var member = symbol.Name.Split('.').Last();
            if (member.Length < MinimumNameLength) continue;

            total += 1;
            if (Regex.IsMatch(corpus, $@"\.{Regex.Escape(member)}\b")) covered += 1;
            else missing.Add(symbol.Name);
        }

        return (total, covered, missing);
    }
    #endregion
}
