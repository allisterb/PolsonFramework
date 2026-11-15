namespace Polson.Tests.Drawing;

using System;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// The mind map as drawn: its names whole and its key only to the marks it uses. The outline is lanternkeepermotion1's
/// (2026-11-14), whose map cut "Coastal unease" to "Coastal u…" and "the beam sweeping" to "the beam…" in pills with room
/// for two lines, and keyed a Qloo mark on a map made without Qloo.
/// </summary>
public class MindMapDrawingTests : TestsRuntime
{
    private const string Outline = """
        # The Lantern Keeper — one last light
        - Synonyms
          - keeper
            - warden
            - watchman
          - lamp
            - beacon
            - lens
        - Antonyms
          - darkness
            - power cut
            - blackout
          - retirement
            - the last shift
        - Clichés
          - storm night
            - rain on glass
          - the beam sweeping
            - ships saved
        - Related words
          - duty
            - climbing the stairs
            - one more time
          - the town below
            - windows going out
        - Research
          - Slow burn
            - "deliberately languid" (Aftersun)
            - "unhurried pace" (Banshees)
          - Coastal unease
            - island isolation
            - inky low light (The Lighthouse)
          - Teaser shape
            - first image breathes
            - title late, card ~3 s
        - Lisbon, in Portuguese
          - faroleiro
            - the trade's own word (assumed)
          - farol
            - lighthouse, also a car's headlight (assumed)
        - Seoul, in Korean
          - 등대
            - lighthouse, a guiding figure (assumed)
          - 정전
            - power cut (assumed)
        """;

    private static string[] Drawn(string outline)
    {
        var canvas = new SkiaCanvas(1600, 1100);
        var ctx = (CanvasRenderingContext2D)canvas.GetContext("2d");
        MindMap.Draw(ctx, MindMap.Parse(outline), 0, 0, 1600, 1100);
        return [.. canvas.DrawnText.Select(t => t.Words)];
    }

    [Fact]
    public void TestAnInnerNameTooWideForOneLineBreaksRatherThanIsCut()
    {
        var drawn = Drawn(Outline);

        Assert.DoesNotContain(drawn, w => w.Contains("Coastal u…", StringComparison.Ordinal) || w.Contains("the beam…", StringComparison.Ordinal));
        Assert.Contains(drawn, w => w.Contains("Coastal", StringComparison.Ordinal));
        Assert.Contains(drawn, w => w.Contains("unease", StringComparison.Ordinal));
        Assert.Contains(drawn, w => w.Contains("sweeping", StringComparison.Ordinal));
    }

    [Fact]
    public void TestTheKeyNamesOnlyTheMarksTheMapUses()
    {
        var drawn = Drawn(Outline);

        Assert.DoesNotContain(drawn, w => w.Contains("Qloo", StringComparison.Ordinal));
        Assert.Contains(drawn, w => w.StartsWith("assumed:", StringComparison.Ordinal));
        Assert.Contains(drawn, w => w == "an association");

        var measured = Drawn("# Centre\n- Audience\n  - Suspiria (t1)\n  - an idea");
        Assert.Contains(measured, w => w.StartsWith("measured:", StringComparison.Ordinal));
        Assert.DoesNotContain(measured, w => w.StartsWith("assumed:", StringComparison.Ordinal));
    }

    /// <summary>Quotes come off a label only when they wrap all of it.</summary>
    [Fact]
    public void TestAQuotedPhraseWithASourceKeepsBothItsQuotes()
    {
        var map = MindMap.Parse("# C\n- Research\n  - \"unhurried pace\" (Banshees)\n  - \"burning the midnight oil\"");

        Assert.Equal(["\"unhurried pace\" (Banshees)", "burning the midnight oil"], map.Children[0].Children.Select(c => c.Text));
    }
}
