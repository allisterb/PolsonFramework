namespace Polson.Tests.Drawing;

using System.Linq;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;

/// <summary>
/// Reading a turnaround sheet when nobody said what is on it: how many figures, and which views they are.
/// </summary>
/// <remarks>
/// <b>Added when the storyboard workflows moved to three views.</b> A sheet asked for a left and a right
/// side view came back on every character of the lastlight run with the front drawn twice, so the
/// workflows now ask for one profile, and <c>GenerateCharacter</c> reads the layout from the figure count
/// instead of assuming four. The failure worth pinning is the silent one: two figures touching read as
/// one, and a count taken from them names every later view wrongly.
/// </remarks>
public class CharacterSheetTests : TestsRuntime
{
    #region Tests
    /// <summary>With no count given, every separated figure is cut.</summary>
    [Fact]
    public void SplitSheet_CountsTheFiguresWhenNoCountIsGiven()
    {
        using var sheet = Sheet((20, 80), (140, 170), (230, 290));

        var figures = CharacterBuilder.SplitSheet(sheet, 0, out var why);

        Assert.Null(why);
        Assert.Equal(3, figures.Count);
    }

    /// <summary>Three figures are front, side, back; four are front, back, left, right; one is a front.</summary>
    [Fact]
    public void OrderOf_NamesTheViewsByHowManyThereAre()
    {
        Assert.Equal(["front", "side", "back"], Order(Sheet((20, 80), (140, 170), (230, 290))));
        Assert.Equal(["front", "back", "left", "right"], Order(Sheet((20, 80), (140, 200), (260, 290), (350, 380))));
        Assert.Equal(["front"], Order(Sheet((20, 80))));
    }

    /// <summary>Two touching figures are refused rather than read as one view.</summary>
    /// <remarks>
    /// A four-view sheet with the front and back touching splits into three, and read as three it would
    /// build the merged pair as the front and the left profile as the back.
    /// </remarks>
    [Fact]
    public void OrderOf_RefusesTwoFiguresTouching()
    {
        using var sheet = Sheet((20, 160), (220, 250), (310, 340));
        var figures = CharacterBuilder.SplitSheet(sheet, 0, out _);

        var order = CharacterBuilder.OrderOf(figures, out var why);

        Assert.Equal(3, figures.Count);
        Assert.Null(order);
        Assert.Contains("touching", why);
    }

    /// <summary>A count with no layout of its own is refused with the count, not guessed at.</summary>
    [Fact]
    public void OrderOf_RefusesACountWithNoDefault()
    {
        using var sheet = Sheet((20, 80), (140, 200));

        var order = CharacterBuilder.OrderOf(CharacterBuilder.SplitSheet(sheet, 0, out _), out var why);

        Assert.Null(order);
        Assert.Contains("2 separate", why);
    }
    #endregion

    #region Helpers
    static string[] Order(SKBitmap sheet)
    {
        using (sheet)
        {
            return CharacterBuilder.OrderOf(CharacterBuilder.SplitSheet(sheet, 0, out _), out _) ?? [];
        }
    }

    /// <summary>Dark upright figures on white, occupying the given column ranges.</summary>
    static SKBitmap Sheet(params (int From, int To)[] figures)
    {
        var bitmap = new SKBitmap(400, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = new SKColor(40, 40, 40) };
        foreach (var (from, to) in figures.Select(f => (f.From, f.To)))
        {
            canvas.DrawRect(SKRect.Create(from, 20, to - from, 160), paint);
        }

        return bitmap;
    }
    #endregion
}
