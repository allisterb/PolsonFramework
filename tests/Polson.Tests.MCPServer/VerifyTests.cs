namespace Polson.Tests.MCPServer;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using global::Polson.Animation;
using global::Polson.ExtendedMind.DocumentProcessing;
using global::Polson.MCPServer;
using global::Polson.Tests;
using Xunit;

/// <summary>
/// <c>Verify.film</c>: a watcher's transcription held against what the composition set, each text's source looked up, and
/// strays kept pending until their frames have been looked at. The watcher is a fake service; the film a real encode.
/// </summary>
public class VerifyTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-verify-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public VerifyTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "documents"));
        File.WriteAllText(Path.Combine(root, "brief.md"),
            "# Brief\n<!-- BRIEF-BEGIN -->\nFilm: Be Cool. Tagline: Everyone wants a piece of the action.\n<!-- BRIEF-END -->\n");
        // A document as Documents.ask leaves it: the file, and its page text cached under the file's hash.
        var pdf = Path.Combine(root, "documents", "letter.pdf");
        File.WriteAllBytes(pdf, [37, 80, 68, 70, 1, 2, 3]);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pdf)))[..32];
        var pages = Directory.CreateDirectory(Path.Combine(root, ".polson", "documents", "pages", hash));
        File.WriteAllText(Path.Combine(pages.FullName, "pages.json"),
            new JsonObject { ["Pages"] = new JsonArray("Cover", "Revenues 105.1 582.3 454% in USD millions; returned $974 million") }.ToJsonString());
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static FilmReadBack.Shown Shown(string text, double from, double to) => new(text, from, to, to - from, [[from, to]]);

    private static FilmReadBack.Reading Read(string text, double at) => new(text, at, at + 1);

    [Fact]
    public void TestATextReadAsSetAtItsTimeIsSeenAndOneNotReadIsMissed()
    {
        var shown = new[] { Shown("BE COOL", 0, 3), Shown("$582.3M", 4, 6), Shown("Q2 2026", 7, 7.4) };
        var outcome = FilmReadBack.Match(shown, [Read("Be Cool", 0.5), Read("$582.3M", 4.2)]);

        Assert.Equal(["Be Cool", "$582.3M", null], outcome.Seen.Select(r => r?.Text));
        Assert.Empty(outcome.Strays);
    }

    /// <summary>
    /// A text of two lines read as two readings is seen whole, with no stray. lanternkeepermotion1 (2026-11-14): the first
    /// line's reading paired with the whole text, and every second line came back as text the film never set.
    /// </summary>
    [Fact]
    public void TestEachLineOfATwoLineTextReadApartIsThatText()
    {
        var shown = new[] { Shown("Na noite em que a vila\nfica às escuras,", 2.6, 6.0), Shown("O FAROLEIRO", 11.5, 15) };
        var outcome = FilmReadBack.Match(shown, [Read("Na noite em que a vila", 3), Read("fica às escuras,", 3.2), Read("O FAROLEIRO", 12)]);

        Assert.Equal("Na noite em que a vila\nfica às escuras,", outcome.Seen[0]?.Text);
        Assert.Empty(outcome.Strays);

        // Read second line first, the reading still runs in the order the lines were set.
        var reversed = FilmReadBack.Match(shown, [Read("fica às escuras,", 3), Read("Na noite em que a vila", 3.2)]);
        Assert.Equal("Na noite em que a vila\nfica às escuras,", reversed.Seen[0]?.Text);
        Assert.Empty(reversed.Strays);

        // A line the film did not set is still a stray, beside a text of two lines or not.
        var ghost = FilmReadBack.Match(shown, [Read("Na noite em que a vila", 3), Read("fica às escuras,", 3.2), Read("Um farol apagado", 4)]);
        Assert.Single(ghost.Strays);
        Assert.Equal("Um farol apagado", ghost.Strays[0].Reading.Text);
    }

    /// <summary>
    /// Lines in other scripts are seen as set, read back the way Nemotron read them in the language probe (2026-10-27):
    /// Hindi without the nukta (बाज़ार as बाजार, an accepted spelling), Chinese with ASCII brackets for full-width ones,
    /// Japanese and Korean exactly. The figure in each still has to be the one set.
    /// </summary>
    [Fact]
    public void TestLinesInOtherScriptsAreSeenAsSetDespiteMarksAndBracketForms()
    {
        var shown = new[]
        {
            Shown("अमेरिकी बाज़ार: 4.2 अरब डॉलर (अनुमान)", 0, 3), Shown("美国市场：42亿美元（估计）", 3, 6),
            Shown("ワンクリックで、車が来る。", 6, 9), Shown("미국 시장: 42억 달러 (추정)", 9, 12),
        };
        var outcome = FilmReadBack.Match(shown, [Read("अमेरिकी बाजार: 4.2 अरब डॉलर (अनुमान)", 1), Read("美国市场： 42亿美元 (估计)", 4),
            Read("ワンクリックで、車が来る。", 7), Read("미국 시장: 42억 달러 (추정)", 10)]);

        Assert.All(outcome.Seen, r => Assert.NotNull(r));
        Assert.Empty(outcome.Strays);

        var wrong = FilmReadBack.Match([Shown("美国市场：42亿美元（估计）", 0, 3)], [Read("美国市场：24亿美元（估计）", 1)]);
        Assert.Null(wrong.Seen[0]);
    }

    /// <summary>
    /// With Nemotron reading, Arabic and Hindi are left to the eye: it read a correct Arabic heading as other words and
    /// becool3's Hindi credits as unrelated sentences. A text in either script is then recognised; others are not.
    /// </summary>
    [Fact]
    public void TestArabicAndHindiAreLeftToTheEyeWhenNemotronReads()
    {
        Assert.True(FilmReadBack.Unreadable("السوق الأمريكية: 4.2 مليار دولار", nemotron: true));
        Assert.True(FilmReadBack.Unreadable("Revenue — الإيرادات", nemotron: true));
        Assert.True(FilmReadBack.Unreadable("अमेरिकी बाज़ार: 4.2", nemotron: true));
        Assert.False(FilmReadBack.Unreadable("美国市场：42亿美元", nemotron: true));
        Assert.False(FilmReadBack.Unreadable("٤٫٢", nemotron: true));     // Arabic-Indic digits alone are a figure, which it reads
        Assert.False(FilmReadBack.Unreadable("४.२", nemotron: true));     // and Devanagari digits
        Assert.Contains("2 texts are in Arabic or Hindi", FilmReadBack.UncheckedNote(2, "frame"));
        Assert.Equal("", FilmReadBack.UncheckedNote(0, "frame"));
    }

    /// <summary>
    /// Gemini is trusted with both: it read becool3's Hindi film almost exactly, and in the Arabic probe (2026-10-29) read six
    /// correct Arabic lines exactly and deliberately wrong ones as drawn. Its readings are checked like any other script's.
    /// </summary>
    [Fact]
    public void TestGeminiLeavesNoScriptToTheEye()
    {
        Assert.False(FilmReadBack.Unreadable("السوق الأمريكية: 4.2 مليار دولار"));
        Assert.False(FilmReadBack.Unreadable("अमेरिकी बाज़ार: 4.2"));
    }

    /// <summary>The Arabic probe's lines, read by Gemini as set: each is seen, the figures and brackets included.</summary>
    [Fact]
    public void TestArabicLinesReadAsSetAreSeen()
    {
        var shown = new[] { Shown("4.2 مليار دولار سنوياً", 0, 4), Shown("حصة أكبر أربع شركات: 22% (من الإيرادات)", 0, 4) };
        var outcome = FilmReadBack.Match(shown, [Read("4.2 مليار دولار سنوياً", 0), Read("حصة أكبر أربع شركات: 22% (من الإيرادات)", 0)]);

        Assert.All(outcome.Seen, r => Assert.NotNull(r));
        Assert.Null(FilmReadBack.Match([Shown("4.2 مليار دولار سنوياً", 0, 4)], [Read("2.4 مليار دولار سنوياً", 0)]).Seen[0]);
    }

    /// <summary>The report film's case: the line was set with $974M and read with $744M. Not a match — a stray to look at.</summary>
    [Fact]
    public void TestTheSameLineWithADifferentFigureIsAStrayNotAMatch()
    {
        var shown = new[] { Shown("Returned $974M to shareholders", 10, 14) };
        var outcome = FilmReadBack.Match(shown, [Read("Returned $744M to shareholders", 11), Read("GHOST", 12)]);

        Assert.Null(outcome.Seen[0]);
        Assert.Equal(2, outcome.Strays.Length);
        Assert.Equal(("different figure", "Returned $974M to shareholders"), (outcome.Strays[0].Kind, outcome.Strays[0].Near));
        Assert.Equal(("never set", null), (outcome.Strays[1].Kind, outcome.Strays[1].Near));
    }

    /// <summary>
    /// nvdatenk1: the watcher wrote each scene as one multi-line block, and a film whose every line was read scored 0 of 92
    /// seen with nine strays. A block that matches nothing is read line by line; a line in it the film never set is still a
    /// stray, on its own.
    /// </summary>
    [Fact]
    public void TestAScenesLinesReadAsOneBlockAreEachSeen()
    {
        var shown = new[] { Shown("Revenue", 5, 15), Shown("$215.9B", 5, 15), Shown("Operating income", 5, 15), Shown("$130.4B", 5, 15) };
        var outcome = FilmReadBack.Match(shown, [Read("Revenue\n$215.9B\nOperating income\n$130.4B", 6)]);

        Assert.Equal(["Revenue", "$215.9B", "Operating income", "$130.4B"], outcome.Seen.Select(r => r?.Text));
        Assert.Empty(outcome.Strays);

        var extra = FilmReadBack.Match(shown, [Read("Revenue\n$215.9B\nOperating income\n$130.4B\nGHOST LINE", 6)]);
        Assert.All(extra.Seen, r => Assert.NotNull(r));
        Assert.Equal("GHOST LINE", Assert.Single(extra.Strays).Reading.Text);
    }

    /// <summary>A block that is one set text of several lines still pairs whole, as it did before blocks were split.</summary>
    [Fact]
    public void TestABlockThatIsOneTextOfSeveralLinesStillPairsWhole()
    {
        var outcome = FilmReadBack.Match([Shown("Unofficial study —\nnot affiliated with the studio.", 10, 15)],
            [Read("Unofficial study —\nnot affiliated with the studio.", 11)]);
        Assert.Equal("Unofficial study —\nnot affiliated with the studio.", outcome.Seen[0]?.Text);
        Assert.Empty(outcome.Strays);
    }

    [Fact]
    public void TestALineReadFarFromItsTimeIsNotItsReadingAndLinesReadApartMakeTheWhole()
    {
        var outcome = FilmReadBack.Match([Shown("Fiscal 2026\nRevenue $215.9B", 20, 25)],
            [Read("Fiscal 2026", 20.5), Read("Revenue $215.9B", 21.5)]);
        Assert.Equal("Fiscal 2026\nRevenue $215.9B", outcome.Seen[0]?.Text);

        var late = FilmReadBack.Match([Shown("TITLE", 0, 2)], [Read("TITLE", 9)]);
        Assert.Null(late.Seen[0]);
        Assert.Single(late.Strays);
    }

    /// <summary>
    /// uberpitch1: six lines read word for word about two seconds before they arrived were each reported twice, missed
    /// and stray. An exact reading that near is the text; a near-miss of the words still has to be in time.
    /// </summary>
    [Fact]
    public void TestAnExactReadingAFewSecondsEarlyIsTheTextNotAMissAndAStray()
    {
        var shown = new[] { Shown("the whole US market, a year", 43.67, 54.92), Shown("Scenarios — the company’s own estimates.", 55.67, 72.9) };
        var outcome = FilmReadBack.Match(shown, [Read("the whole US market, a year", 42), Read("Scenarios - the company's own estimates.", 54)]);

        Assert.All(outcome.Seen, Assert.NotNull);
        Assert.Empty(outcome.Strays);

        var near = FilmReadBack.Match([Shown("the whole US market, a year", 43.67, 54.92)], [Read("the whole market a year", 41)]);
        Assert.Null(near.Seen[0]);
        Assert.Single(near.Strays);
    }

    /// <summary>
    /// nightshift1: the watcher read every line right and dated it in the wrong unit — each true time over ten, then over a
    /// hundred. Read at the scale that matches, every line is seen; a reading already in seconds is left alone.
    /// </summary>
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.01)]
    public void TestTimesInTheWrongUnitAreReadInTheOneThatMatches(double unit)
    {
        var shown = new[] { Shown("NIGHTSHIFT", 5.0, 8.67), Shown("COLD BREW", 6.25, 8.67), Shown("Steeped for twenty hours.", 8.83, 11.92),
            Shown("Find it chilled.", 13.0, 15.0) };
        FilmReadBack.Reading Scaled(string text, double at) => new(text, at * unit, (at + 1) * unit);
        var read = new[] { Scaled("NIGHTSHIFT", 5.2), Scaled("COLD BREW", 6.4), Scaled("Steeped for twenty hours.", 9.0), Scaled("Find it chilled.", 13.2) };

        var scale = FilmReadBack.TimeScale(shown, read, 15);

        Assert.Equal(1 / unit, scale, 3);
        var outcome = FilmReadBack.Match(shown, [.. read.Select(r => r with { From = r.From * scale, To = r.To * scale })]);
        Assert.All(outcome.Seen, Assert.NotNull);
        Assert.Empty(outcome.Strays);
    }

    /// <summary>
    /// becool3's Hindi film: every reading dated 0.02–0.1 s ("0:09" written 0.09) and its Devanagari lines misread, so
    /// only BE COOL could line up once rescaled. As read it saw nothing, so that one line is enough to read it ×100.
    /// </summary>
    [Fact]
    public void TestAReadingThatSeesNothingIsRescaledOnOneLine()
    {
        var shown = new[] { Shown("वह फ़िल्मों की दुनिया छोड़कर", 2, 6), Shown("BE COOL", 9.4, 15), Shown("2005 · निर्देशक: एफ़. गैरी ग्रे", 10.4, 15) };
        FilmReadBack.Reading At(string text, double at) => new(text, at, at + 0.01);
        var read = new[] { At("वह फ़िल्मों की दुनिया फोड़कर सिगरेट के धुएं में", 0.02), At("BE COOL", 0.09),
            At("2005 : निर्माता एक्स ने इन इन एसोसिएशन प्रस्तुत", 0.1) };

        Assert.Equal(100d, FilmReadBack.TimeScale(shown, read, 15));
    }

    /// <summary>
    /// becool3: lines set one under another came back as one string joined by spaces. The credits were three texts missed
    /// and a stray; "HE LEFT THE MOVIES" was missed because the reading of both lines paired with the second alone.
    /// </summary>
    [Fact]
    public void TestSetTextsReadRunTogetherAreEachSeen()
    {
        var shown = new[] { Shown("HE LEFT THE MOVIES", 2, 6), Shown("FOR THE MUSIC BUSINESS", 2.25, 6), Shown("BE COOL", 9.4, 15),
            Shown("2005 · DIREÇÃO: F. GARY GRAY", 10.4, 15), Shown("BASEADO NO ROMANCE DE ELMORE LEONARD", 10.4, 15),
            Shown("Estudo não oficial — sem vínculo com o estúdio ou os distribuidores do filme.", 10.4, 15) };
        var read = new[] { Read("HE LEFT THE MOVIES FOR THE MUSIC BUSINESS", 2.5), Read("BE COOL", 9.5),
            Read("2005 - DIREÇÃO: F GARY GRAY BASEADO NO ROMANCE DE ELMORE LEONARD Estudo não oficial - sem vínculo com o estúdio ou os distribuidores do filme", 10) };

        var outcome = FilmReadBack.Match(shown, read);

        Assert.All(outcome.Seen, Assert.NotNull);
        Assert.Empty(outcome.Strays);

        // A text found in a reading that is mostly something else is seen, and the reading stays a stray to look at.
        var mixed = FilmReadBack.Match(shown[..2], [Read("HE LEFT THE MOVIES FOR THE MUSIC BUSINESS AND THEN SOMETHING NEVER SET ON SCREEN", 2.5)]);
        Assert.All(mixed.Seen, Assert.NotNull);
        Assert.Single(mixed.Strays);
    }

    [Fact]
    public void TestAReadingInSecondsIsNotStretchedToFitEvenWithAMiss()
    {
        var shown = new[] { Shown("TITLE", 0.5, 3), Shown("A LINE", 4, 6), Shown("END", 7, 9.5) };
        var read = new[] { Read("TITLE", 1), Read("END", 7.5) };              // "A LINE" missed, honestly

        Assert.Equal(1d, FilmReadBack.TimeScale(shown, read, 10));
        Assert.Equal(1d, FilmReadBack.TimeScale(shown, [], 10));
    }

    [Fact]
    public void TestTheFilmQuestionStatesTheUnitAndTheSchemaBoundsIt()
    {
        Assert.Contains("The video is 15 seconds long", VerifyToolkit.Question(15));
        Assert.Contains("from 5, to 8.5", VerifyToolkit.Question(15));
        Assert.Contains("\"maximum\":87.25", VerifyToolkit.Schema(87.25));
        System.Text.Json.Nodes.JsonNode.Parse(VerifyToolkit.Schema(15));     // still a valid schema
    }

    [Theory]
    [InlineData("$40.4 billion (282 million)", new[] { "40.4", "282" })]
    [InlineData("102,718 units, up 4.5%.", new[] { "102718", "4.5" })]
    public void TestFiguresAreReadWithoutTheirCommas(string text, string[] figures) =>
        Assert.Equal(figures, FilmReadBack.Figures(text));

    [Fact]
    public void TestEachTextsSourceIsLookedUpNotAsked()
    {
        var session = new SessionContext();
        session.Taste.Add(id => new global::Polson.ExtendedMind.Taste.TasteResult
        {
            Id = id, Op = "profile", Label = "audience A", Ok = true,
            Entities = [new Dictionary<string, object?> { ["id"] = "e1", ["name"] = "Riverside Mukolitta" }],
        });
        var sources = new FilmSources(root, session);
        var computed = new Dictionary<string, string> { ["+46%"] = "582.3 / 399.0 − 1" };

        Assert.Equal(("brief", null), sources.Classify("Everyone wants a piece of the action", computed));
        Assert.Equal(("document", "documents/letter.pdf p. 2"), sources.Classify("$582.3M", computed));
        Assert.Equal(("document", "documents/letter.pdf p. 2"), sources.Classify("$974 million returned", computed));
        Assert.Equal(("computed", "582.3 / 399.0 − 1"), sources.Classify("+46%", computed));
        Assert.Equal(("qloo", "t1"), sources.Classify("Riverside Mukolitta", computed));
        Assert.Equal(("own words", null), sources.Classify("Coming soon", computed));
        Assert.Equal(("unsourced figure", "no source for 183.4"), sources.Classify("$183.4B profit", computed));
    }

    /// <summary>
    /// A research figure with a thousands separator is found in the research. anthropic_s_1_outside_cc set Alphabet's
    /// $229,692M from a research answer printing "$229,692 million", and all three such figures came back unsourced:
    /// the research text was matched after its comma had become a space.
    /// </summary>
    [Fact]
    public void TestAResearchFigureWithAThousandsSeparatorIsSourced()
    {
        var session = new SessionContext();
        var task = session.Research.Start("trun_1", "peers", "Alphabet's six months to June 30, 2026", "core", "running");
        session.Research.Complete(task, new Dictionary<string, object?>
        {
            ["output"] = "the Consolidated Statements of Income report “Revenues” of $229,692 million and “Income from operations” of 80,466 million",
        }, []);
        var sources = new FilmSources(root, session);
        var none = new Dictionary<string, string>();

        Assert.Equal(("research", "trun_1"), sources.Classify("$229,692M", none));
        Assert.Equal(("research", "trun_1"), sources.Classify("Alphabet  +$80,466M", none));
        Assert.Equal(("unsourced figure", "no source for 44796"), sources.Classify("$44,796M", none));
    }

    /// <summary>
    /// A read-back that compared nothing vouches for nothing. coffee_1 failed its read-back on 15 unsourced figures, then
    /// recorded a pass on "0 texts on the image", and the page called every figure sourced.
    /// </summary>
    [Fact]
    public void TestAReadBackThatReadNothingFails()
    {
        var empty = new ImageCheck("artifacts/final_a.png", "canvas", 1200, 1800, [], [], "reader", null).Result();
        Assert.False((bool)empty["passed"]!);
        Assert.Contains("Nothing was checked", (string)empty["summary"]!);

        var one = new ImageCheck("artifacts/final_a.png", "canvas", 1200, 1800,
            [new Dictionary<string, object?> { ["text"] = "Nightshift", ["source"] = "brief", ["status"] = "seen" }], [], "reader", null).Result();
        Assert.True((bool)one["passed"]!);
    }

    /// <summary>A sentence is sourced figure by figure: a document's figure and a computed one in one line, from two pages.</summary>
    [Fact]
    public void TestASentenceIsSourcedFigureByFigure()
    {
        var sources = new FilmSources(root, null);
        var computed = new Dictionary<string, string> { ["Top 4.4%"] = "22 / 500" };

        Assert.Equal(("computed", "documents/letter.pdf p. 2; 4.4 = 22 / 500"),
            sources.Classify("Returned $974 million, the top 4.4%", computed));
        Assert.Equal(("unsourced figure", "no source for 7"), sources.Classify("$974 million in 7 days", computed));
        Assert.Equal(("scale", null), sources.Classify("1,000%", computed, new HashSet<string> { "1,000%" }));
    }

    /// <summary>
    /// End to end on a real encode: one text read as set, one read with another figure, one reading of nothing set. The
    /// strays' frames are saved and they stay pending — the check does not pass — until settled after looking.
    /// </summary>
    [Fact]
    public async Task TestAFilmIsCheckedAndItsStraysWaitForTheirFrames()
    {
        if (!FFmpeg.Available || !SkiaSharp.SKFontManager.Default.GetFontFamilies().Contains("Arial"))
        {
            Info("Skipped: needs ffmpeg and Arial.");
            return;
        }
        Info("ffmpeg: {0}", FFmpeg.Executable!);
        var motion = new MotionToolkit(root);
        var comp = motion.Composition(new Hashtable { ["width"] = 320d, ["height"] = 180d, ["fps"] = 12d, ["duration"] = 3d });
        comp.Fill(new Hashtable { ["color"] = "#ffffff" });
        comp.Text(new Hashtable { ["text"] = "BE COOL", ["family"] = "Arial", ["size"] = 40d, ["origin"] = new[] { 160d, 50d } });
        comp.Text(new Hashtable { ["text"] = "Returned $974 million", ["family"] = "Arial", ["size"] = 20d, ["origin"] = new[] { 160d, 120d } });
        comp.SaveVideo("output.mp4");

        var watched = new JsonObject
        {
            ["items"] = new JsonArray(
                new JsonObject { ["text"] = "BE COOL", ["from"] = 0, ["to"] = 3 },
                new JsonObject { ["text"] = "Returned $744 million", ["from"] = 1, ["to"] = 3 },
                new JsonObject { ["text"] = "GHOST", ["from"] = 2, ["to"] = 3 }),
        }.ToJsonString();
        using var video = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(new Answer(watched)));
        var recorded = new List<string>();
        var session = new SessionContext();
        var verify = new VerifyToolkit(video, root, session, (type, _) => recorded.Add(type));

        var result = await verify.Film(comp, "output.mp4");

        Assert.True((bool)result["ok"]!, result.GetValueOrDefault("error") as string);
        var items = ((object?[])result["items"]!).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal(["seen", "missed"], items.Select(i => (string)i["status"]!));
        Assert.Equal(["brief", "document"], items.Select(i => (string)i["source"]!));
        var strays = ((object?[])result["strays"]!).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal(["different figure", "never set"], strays.Select(s => (string)s["kind"]!));
        Assert.All(strays, s => Assert.True(File.Exists(Path.Combine(root, (string)s["frame"]!)), $"no frame: {s["frameError"]}"));
        Assert.False((bool)result["passed"]!);
        Assert.Contains("Verify.stray('output.mp4', n, 'misread' | 'real')", (string)result["summary"]!);

        Assert.Throws<InvalidOperationException>(() => verify.Stray(1, "misread"));   // not in the script that ran it
        var later = new VerifyToolkit(video, root, session, (type, _) => recorded.Add(type));
        later.Stray(1, "misread", "the frame shows $974 million");
        var settled = later.Stray(2, "real");
        Assert.False((bool)settled["passed"]!);                            // a real fault in the film still fails it
        Assert.Contains("1 confirmed fault", (string)settled["summary"]!);
        Assert.Equal(["film.verify", "check", "film.stray", "check", "film.stray", "check"], recorded);
        Assert.Throws<ArgumentException>(() => later.Stray(9, "misread"));
        Assert.Throws<ArgumentException>(() => later.Stray(1, "maybe"));
    }

    /// <summary>
    /// becool3: films B then C checked in one session. C's <c>stray-1.png</c> overwrote B's, and B's strays could not be
    /// settled once C was the latest check. Each film's frames carry its name, and a named stray is settled whichever
    /// film was checked last.
    /// </summary>
    [Fact]
    public async Task TestEachFilmKeepsItsOwnStraysAndTheyAreSettledByName()
    {
        if (!FFmpeg.Available || !SkiaSharp.SKFontManager.Default.GetFontFamilies().Contains("Arial"))
        {
            Info("Skipped: needs ffmpeg and Arial.");
            return;
        }
        var motion = new MotionToolkit(root);
        var comp = motion.Composition(new Hashtable { ["width"] = 320d, ["height"] = 180d, ["fps"] = 12d, ["duration"] = 3d });
        comp.Fill(new Hashtable { ["color"] = "#ffffff" });
        comp.Text(new Hashtable { ["text"] = "BE COOL", ["family"] = "Arial", ["size"] = 40d, ["origin"] = new[] { 160d, 90d } });
        comp.SaveVideo("artifacts/final_b.mp4");
        comp.SaveVideo("artifacts/final_c.mp4");

        var watched = new JsonObject
        {
            ["items"] = new JsonArray(new JsonObject { ["text"] = "BE COOL", ["from"] = 0, ["to"] = 3 },
                new JsonObject { ["text"] = "GHOST", ["from"] = 1, ["to"] = 2 }),
        }.ToJsonString();
        using var video = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(new Answer(watched)));
        var session = new SessionContext();
        var verify = new VerifyToolkit(video, root, session);
        var b = await verify.Film(comp, "artifacts/final_b.mp4");
        var c = await verify.Film(comp, "artifacts/final_c.mp4");

        string Frame(Dictionary<string, object?> r) => (string)((object?[])r["strays"]!).Cast<Dictionary<string, object?>>().Single()["frame"]!;
        Assert.Equal("artifacts/verify/final_b-stray-1.png", Frame(b));
        Assert.Equal("artifacts/verify/final_c-stray-1.png", Frame(c));
        Assert.True(File.Exists(Path.Combine(root, "artifacts", "verify", "final_b-stray-1.png")));
        Assert.Throws<InvalidOperationException>(() => verify.Stray("final_b", 1, "misread"));   // not in the script that ran it

        var later = new VerifyToolkit(video, root, session);
        var settled = later.Stray("artifacts/final_b.mp4", 1, "misread", "the frame shows only BE COOL");
        object? Verdict(Dictionary<string, object?> r) => ((object?[])r["strays"]!).Cast<Dictionary<string, object?>>().Single()["verdict"];
        Assert.Equal("misread", Verdict(settled));
        Assert.Equal("real", Verdict(later.Stray(1, "real")));             // the unnamed form still settles the latest check, C
        Assert.Equal("misread", Verdict(later.Stray("final_b", 1, "misread")));   // and B's verdict was kept, not C's
        var unknown = Assert.Throws<ArgumentException>(() => later.Stray("final_a", 1, "misread"));
        Assert.Contains("'artifacts/final_b.mp4'", unknown.Message);
        var beyond = Assert.Throws<ArgumentException>(() => later.Stray(2, "misread"));
        Assert.Contains("Verify.stray('artifacts/final_b.mp4', n, 'misread')", beyond.Message);
    }

    /// <summary>
    /// A delivered film is measured against the brief's sizes and the result recorded as a check of its own; the read-back's
    /// verdict stays about its text, and a draft is not measured. lanternkeeperadk3 delivered 1280 × 720 to a 1920 × 1080 brief.
    /// </summary>
    [Fact]
    public async Task TestADeliveredFilmIsMeasuredAgainstTheBriefsSizes()
    {
        if (!FFmpeg.Available || !SkiaSharp.SKFontManager.Default.GetFontFamilies().Contains("Arial")) { Info("Skipped: needs ffmpeg and Arial."); return; }
        File.WriteAllText(Path.Combine(root, "brief.md"), "# Brief\n\n- Sizes: Landscape 320 × 180 px\n");
        MotionComposition Film(double w, double h)
        {
            var comp = new MotionToolkit(root).Composition(new Hashtable { ["width"] = w, ["height"] = h, ["fps"] = 12d, ["duration"] = 2d });
            comp.Fill(new Hashtable { ["color"] = "#ffffff" });
            comp.Text(new Hashtable { ["text"] = "BE COOL", ["family"] = "Arial", ["size"] = 30d, ["origin"] = new[] { w / 2, h / 2 } });
            return comp;
        }
        var small = Film(256, 144);
        small.SaveVideo("artifacts/final_a.mp4");
        small.SaveVideo("artifacts/draft_a.mp4");
        var right = Film(320, 180);
        right.SaveVideo("artifacts/final_b.mp4");

        var events = new List<(string Type, IReadOnlyDictionary<string, object?> Data)>();
        var watched = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["text"] = "BE COOL", ["from"] = 0, ["to"] = 2 }) }.ToJsonString();
        using var video = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(new Answer(watched)));
        var verify = new VerifyToolkit(video, root, null, (type, data) => events.Add((type, data)));
        IReadOnlyDictionary<string, object?>? SizeCheck(string film) =>
            events.LastOrDefault(e => e.Type == "check" && (string?)e.Data["claim"] == VerifyToolkit.SizeClaim(film)).Data;

        var a = await verify.Film(small, "artifacts/final_a.mp4");
        Assert.True((bool)a["passed"]!, (string)a["summary"]!);             // the text was read as set
        Assert.Contains("artifacts/final_a.mp4 is 256 × 144 px, and the brief asks for 320 × 180", (string)a["summary"]!);
        Assert.Equal(false, SizeCheck("artifacts/final_a.mp4")?["passed"]);
        Assert.Equal("256 × 144", events.Last(e => e.Type == "film.verify").Data["size"]);

        var b = await verify.Film(right, "artifacts/final_b.mp4");
        Assert.DoesNotContain("the brief asks for", (string)b["summary"]!);
        Assert.Equal(true, SizeCheck("artifacts/final_b.mp4")?["passed"]);

        var draft = await verify.Film(small, "artifacts/draft_a.mp4");
        Assert.DoesNotContain("the brief asks for", (string)draft["summary"]!);
        Assert.Null(SizeCheck("artifacts/draft_a.mp4"));
    }

    /// <summary>A film of three lines and a source line, encoded at 320 × 180 for 3 s, and the reader's answer to it.</summary>
    private async Task<Dictionary<string, object?>> CheckFilm(string name, JsonArray read)
    {
        var motion = new MotionToolkit(root);
        var comp = motion.Composition(new Hashtable { ["width"] = 320d, ["height"] = 180d, ["fps"] = 12d, ["duration"] = 3d });
        comp.Fill(new Hashtable { ["color"] = "#ffffff" });
        comp.Text(new Hashtable { ["text"] = "BE COOL", ["family"] = "Arial", ["size"] = 30d, ["origin"] = new[] { 160d, 30d } });
        comp.Text(new Hashtable { ["text"] = "Returned $974 million", ["family"] = "Arial", ["size"] = 16d, ["origin"] = new[] { 160d, 70d } });
        comp.Text(new Hashtable { ["text"] = "Revenues rose", ["family"] = "Arial", ["size"] = 16d, ["origin"] = new[] { 160d, 100d } });
        comp.Text(new Hashtable { ["text"] = "Shareholder letter, p. 2", ["family"] = "Arial", ["size"] = 9d, ["origin"] = new[] { 160d, 160d },
            ["desc"] = "source line" });
        comp.SaveVideo(name);
        using var video = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(new Answer(new JsonObject { ["items"] = read }.ToJsonString())));
        return await new VerifyToolkit(video, root, null).Film(comp, name);
    }

    /// <summary>
    /// A source line (<c>desc: 'source…'</c>) is a citation, set at footnote size: neither graded nor a stray when the reader
    /// gets its page wrong, as small type is misread. The texts it cites are checked as themselves.
    /// </summary>
    [Fact]
    public async Task TestASourceLineIsACitationNotGradedAndItsMisreadingIsNoStray()
    {
        if (!FFmpeg.Available || !SkiaSharp.SKFontManager.Default.GetFontFamilies().Contains("Arial")) { Info("Skipped: needs ffmpeg and Arial."); return; }

        var result = await CheckFilm("cited.mp4", new JsonArray(
            new JsonObject { ["text"] = "BE COOL\nReturned $974 million\nRevenues rose\nShareholder letter, p. 7", ["from"] = 0, ["to"] = 3 }));

        Assert.True((bool)result["ok"]!, result.GetValueOrDefault("error") as string);
        var items = ((object?[])result["items"]!).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal(["seen", "seen", "seen", "citation"], items.Select(i => (string)i["status"]!));
        Assert.Equal("citation", items[3]["source"]);
        Assert.Empty((object?[])result["strays"]!);
        Assert.True((bool)result["passed"]!, (string)result["summary"]!);
    }

    /// <summary>
    /// nvdatenk1 passed with 0 of 92 texts seen: coverage was not part of the verdict. A reader that saw under half of what it
    /// could check cannot vouch for the film, and the check says so rather than passing.
    /// </summary>
    [Fact]
    public async Task TestAReadBackThatSawUnderHalfTheTextsDoesNotPass()
    {
        if (!FFmpeg.Available || !SkiaSharp.SKFontManager.Default.GetFontFamilies().Contains("Arial")) { Info("Skipped: needs ffmpeg and Arial."); return; }

        var half = await CheckFilm("half.mp4", new JsonArray(new JsonObject { ["text"] = "BE COOL", ["from"] = 0, ["to"] = 3 }));
        Assert.False((bool)half["passed"]!);
        Assert.Contains("saw 1 of the 3 texts it could check, under half", (string)half["summary"]!);

        var most = await CheckFilm("most.mp4", new JsonArray(new JsonObject { ["text"] = "BE COOL", ["from"] = 0, ["to"] = 3 },
            new JsonObject { ["text"] = "Revenues rose", ["from"] = 0, ["to"] = 3 }));
        Assert.True((bool)most["passed"]!, (string)most["summary"]!);
    }

    /// <summary>
    /// A film over the watcher's limit is watched as a smaller copy, and the summary says so. becool2's 65 MB master was
    /// refused outright, and the agent made a copy by hand after two refusals.
    /// </summary>
    [Fact]
    public async Task TestAFilmOverTheLimitIsWatchedAsACopy()
    {
        if (!FFmpeg.Available || !SkiaSharp.SKFontManager.Default.GetFontFamilies().Contains("Arial"))
        {
            Info("Skipped: needs ffmpeg and Arial.");
            return;
        }
        var motion = new MotionToolkit(root);
        var comp = motion.Composition(new Hashtable { ["width"] = 320d, ["height"] = 180d, ["fps"] = 12d, ["duration"] = 3d });
        comp.Fill(new Hashtable { ["color"] = "#ffffff" });
        comp.Text(new Hashtable { ["text"] = "BE COOL", ["family"] = "Arial", ["size"] = 40d, ["origin"] = new[] { 160d, 90d } });
        comp.SaveVideo("big.mp4");
        var limit = (int)(new FileInfo(Path.Combine(root, "big.mp4")).Length * 9 / 10);

        var answer = new Answer(new JsonObject { ["items"] = new JsonArray(new JsonObject { ["text"] = "BE COOL", ["from"] = 0, ["to"] = 3 }) }.ToJsonString());
        using var video = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(answer)) { MaxClipBytes = limit };
        var result = await new VerifyToolkit(video, root, null).Film(comp, "big.mp4");

        Assert.True((bool)result["ok"]!, result.GetValueOrDefault("error") as string);
        var copy = Path.Combine(root, "artifacts", "verify", "big-watch.mp4");
        Assert.InRange(new FileInfo(copy).Length, 1, limit);
        var sent = answer.LastRequest.Replace("\\u002B", "+").Replace("\\/", "/");
        Assert.Contains(Convert.ToBase64String(File.ReadAllBytes(copy))[..400], sent);                   // the copy was sent
        Assert.Equal("big.mp4", result["video"]);
        Assert.Contains("Read from artifacts/verify/big-watch.mp4", (string)result["summary"]!);
        Assert.True((bool)result["passed"]!);
    }
    /// <summary>
    /// A counting number is checked by the values it holds. A reading in its format and range while it counts is no stray,
    /// even a value no frame drew exactly; one outside its range, or after it stopped, is.
    /// </summary>
    [Fact]
    public async Task TestACountingNumberIsReadByWhatItHolds()
    {
        if (!FFmpeg.Available || !SkiaSharp.SKFontManager.Default.GetFontFamilies().Contains("Arial"))
        {
            Info("Skipped: needs ffmpeg and Arial.");
            return;
        }
        var motion = new MotionToolkit(root);
        var comp = motion.Composition(new Hashtable { ["width"] = 320d, ["height"] = 180d, ["fps"] = 12d, ["duration"] = 4d });
        comp.Fill(new Hashtable { ["color"] = "#ffffff" });
        var count = motion.Nodes.Animated("real", new object[]
        {
            new Hashtable { ["time"] = 0d, ["value"] = 0d }, new Hashtable { ["time"] = 1d, ["value"] = 0d }, new Hashtable { ["time"] = 2.5, ["value"] = 974d },
        });
        var number = comp.Number(new Hashtable { ["value"] = count, ["family"] = "Arial", ["size"] = 40d, ["origin"] = new[] { 160d, 90d },
            ["format"] = new Hashtable { ["prefix"] = "$", ["suffix"] = "M" } });
        comp.SaveVideo("counting.mp4");

        var watched = new JsonObject
        {
            ["items"] = new JsonArray(
                new JsonObject { ["text"] = "$0M", ["from"] = 0, ["to"] = 1 },
                new JsonObject { ["text"] = number.TextAt(1.75), ["from"] = 1.75, ["to"] = 1.8 },
                new JsonObject { ["text"] = "$380M", ["from"] = 1.5, ["to"] = 1.5 },
                new JsonObject { ["text"] = "$974M", ["from"] = 2.5, ["to"] = 4 },
                new JsonObject { ["text"] = "$999M", ["from"] = 3, ["to"] = 3.5 }),
        }.ToJsonString();
        using var video = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(new Answer(watched)));
        var result = await new VerifyToolkit(video, root, null).Film(comp, "counting.mp4", new Dictionary<string, object?>
        {
            ["computed"] = new Dictionary<string, object?> { ["$0M"] = "the start", ["$974M"] = "974" },
        });

        Assert.True((bool)result["ok"]!, result.GetValueOrDefault("error") as string);
        var items = ((object?[])result["items"]!).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal(["$0M", "$974M"], items.Select(i => (string)i["text"]!));
        Assert.All(items, i => Assert.Equal("seen", i["status"]));
        var stray = Assert.Single(((object?[])result["strays"]!).Cast<Dictionary<string, object?>>());
        Assert.Equal("$999M", stray["read"]);
    }

    [Fact]
    public void TestAStillsTextsAreReadOnceEachAndAWrappedReadingCoversItsLines()
    {
        string[] texts = ["Qloo", "2,205.8%", "Growth 2016-19", "Fastest-growing companies", "in the Americas"];
        var (seen, _, strays) = ImageReadBack.Match(texts,
            ["QLOO", "2,205.8%", "Growth 2016–19", "Fastest-growing companies in the Americas"]);

        Assert.Equal(["QLOO", "2,205.8%", "Growth 2016–19", "Fastest-growing companies in the Americas", "Fastest-growing companies in the Americas"], seen.AsEnumerable());
        Assert.Empty(strays);
    }

    /// <summary>A figure read differently is a stray beside the line it resembles, alone or inside a run-together reading.</summary>
    [Fact]
    public void TestAStillsFigureReadDifferentlyIsAStrayBesideItsLine()
    {
        var alone = ImageReadBack.Match(["Grew 2,205.8%", "Rank 22"], ["Grew 2,206.8%", "Rank 22", "Statista"]);
        Assert.Equal([null, "Rank 22"], alone.Seen.AsEnumerable());
        Assert.Equal([("different figure", "Grew 2,205.8%"), ("never set", null)], alone.Strays.Select(s => (s.Kind, s.Near)));

        var together = ImageReadBack.Match(["Qloo", "Technology", "2,205.8%"], ["Qloo Technology 2,205.6%"]);
        Assert.Equal(["Qloo Technology 2,205.6%", "Qloo Technology 2,205.6%", null], together.Seen.AsEnumerable());
        Assert.Equal([("different figure", "2,205.8%")], together.Strays.Select(s => (s.Kind, s.Near)));
    }

    /// <summary>
    /// qloo_among_the_fastest_growing: the reader listed each bar's figure on its own and again inside a table row read
    /// across its columns. Every line in the row was drawn, so the row is explained, not a stray; a name read short is
    /// missed, with the reading closest to it.
    /// </summary>
    [Fact]
    public void TestATableRowReadAcrossItsColumnsIsNotAStray()
    {
        string[] texts = ["#18", "Fair Price Properties", "Property · US", "+2,587.2%", "#26", "ORORO Heated Apparel", "Fashion · US"];
        var (seen, closest, strays) = ImageReadBack.Match(texts,
            ["+2,587.2%", "Fair Price Properties", "#18 Fair Price Properties Property • US +2,587.2%",
             "#26 OROO Heated Apparel Fashion • US"]);

        Assert.Empty(strays);
        Assert.Null(seen[5]);
        Assert.Equal("#26 OROO Heated Apparel Fashion • US", closest[5]);
    }

    /// <summary>The equation probe's defect: a split-off fragment, <c>2</c>, is not a reading of a long line that contains it.</summary>
    [Fact]
    public void TestAShortFragmentIsNotAReadingOfTheLongLineItSitsIn()
    {
        var (seen, _, strays) = ImageReadBack.Match(["aₙ = a₁ + (n − 2)d"], ["2"]);
        Assert.Null(seen[0]);
        Assert.Single(strays);
    }

    /// <summary>A script written <c>^</c> is the same text as one set as a superscript character, and a changed exponent is a changed figure.</summary>
    [Fact]
    public void TestScriptsCompareAlikeAndAChangedExponentIsADifferentFigure()
    {
        Assert.Equal("E = mc^2 - a_n", FilmReadBack.Canon("E = mc² − aₙ"));
        var same = ImageReadBack.Match(["E = mc²"], ["E = mc^2"]);
        Assert.Equal("E = mc^2", same.Seen[0]);
        var changed = ImageReadBack.Match(["E = mc²"], ["E = mc^3"]);
        Assert.Null(changed.Seen[0]);
        Assert.Equal([("different figure", "E = mc²")], changed.Strays.Select(s => (s.Kind, s.Near)));
    }

    /// <summary>Readings in LaTeX become linear forms through the same parser; a built-up equation split into rows is joined.</summary>
    [Fact]
    public void TestLatexReadingsAreLinearisedAndSplitEquationsJoined()
    {
        var prepared = MathReading.Prepare([
            @"x = \frac{-b \pm \sqrt{b^2 - 5ac}", "2a",
            @"\int_{0}^{1} x^2 \, dx = \frac{1}", "4",
            "Revenue grew 22%",
            @"a^2 + b^2 = c^2"]);

        Assert.Equal(["x = (−b ± √(b² − 5ac))/(2a)", "∫₀¹ x² dx = 1/4", "Revenue grew 22%", "a² + b² = c²"], prepared.Select(p => p.Compare));
        Assert.Equal([(0, 1), (2, 3), (4, 4), (5, 5)], prepared.Select(p => (p.First, p.Last)));
        Assert.Equal("x = \\frac{-b \\pm \\sqrt{b^2 - 5ac}\n2a", prepared[0].Raw);
    }

    /// <summary>
    /// Readings as the reader sent them on 2026-10-23: wrapped in <c>$…$</c>, and with <c>\begin</c> turned into a backspace by
    /// a JSON string that did not double its backslashes. A price is not math and keeps its dollar sign.
    /// </summary>
    [Theory]
    [InlineData("$E = mc^3$", "E = mc^3")]
    [InlineData("\begin{pmatrix} 2 & 7 \\\\ 0 & 3 \\end{pmatrix}", @"\begin{pmatrix} 2 & 7 \\ 0 & 3 \end{pmatrix}")]
    [InlineData("a \times b", @"a \times b")]
    [InlineData(@"\[ x^2 \]", "x^2")]
    [InlineData("$974M returned", "$974M returned")]
    public void TestAReadingIsUnwrappedAndItsEscapesRepaired(string read, string meant) =>
        Assert.Equal(meant, MathReading.Unwrap(read));

    /// <summary>
    /// The fault probe (2026-10-23): a denominator painted over was read as <c>\frac{1}{}</c>. The stray belongs beside its
    /// equation, so its crop shows where to look.
    /// </summary>
    [Fact]
    public void TestAnEquationWithAPartMissingIsAStrayBesideIt()
    {
        var prepared = MathReading.Prepare([@"\int_0^1 x^2 dx = \frac{1}{}"]);
        var (seen, _, strays) = ImageReadBack.Match(["∫₀¹ x² dx = 1/3", "a² + b² = c²"], [prepared[0].Compare]);
        Assert.Null(seen[0]);
        Assert.Equal([("different figure", "∫₀¹ x² dx = 1/3")], strays.Select(s => (s.Kind, s.Near)));
    }

    /// <summary>
    /// A sign is part of a figure (the axes film read −1 as 1, 2026-10-23); a binary minus and a year range's hyphen are not.
    /// </summary>
    [Fact]
    public void TestASignIsPartOfAFigure()
    {
        Assert.Equal(["-1"], FilmReadBack.MatchFigures("−1"));
        Assert.Equal(["2", "4"], FilmReadBack.MatchFigures("b² − 4ac"));
        Assert.Equal(["2016", "19"], FilmReadBack.MatchFigures("2016-19"));
        Assert.Equal(["-3"], FilmReadBack.MatchFigures("(−3%)"));

        var (seen, _, strays) = ImageReadBack.Match(["−1", "Lost −$974M"], ["1", "Lost $974M"]);
        Assert.Equal([null, null], seen.AsEnumerable());
        Assert.Equal(2, strays.Length);
    }

    /// <summary>The same characters spaced differently are the same reading: <c>x² dx</c> and <c>x^2dx</c>; different figures are not.</summary>
    [Fact]
    public void TestSpacingAloneDoesNotMakeAReadingDifferent()
    {
        Assert.Equal("∫_0^1 x^2dx = 1/3", ImageReadBack.Match(["∫₀¹ x² dx = 1/3"], ["∫_0^1 x^2dx = 1/3"]).Seen[0]);
        Assert.Null(ImageReadBack.Match(["x = 1 2"], ["x = 12"]).Seen[0]);
    }

    /// <summary>
    /// End to end on a real canvas: an equation read back in LaTeX is seen, shown as read; the same equation drawn with
    /// 4ac and read with 5ac is a different figure.
    /// </summary>
    [Fact]
    public async Task TestAnEquationReadBackInLatexIsSeenAndAChangedTermIsAStray()
    {
        var canvas = new global::Polson.Drawing.Skia.SkiaCanvas(900, 400);
        var ctx = canvas.GetContext();
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(0, 0, 900, 400);
        ctx.FillStyle = "#111111";
        ctx.FillEquation(@"E = mc^2", 40, 100, new Hashtable { ["size"] = 48f });
        ctx.FillEquation(@"x = \frac{-b \pm \sqrt{b^2 - 4ac}}{2a}", 40, 280, new Hashtable { ["size"] = 48f });

        var read = new JsonObject
        {
            ["items"] = new JsonArray(
                new JsonObject { ["text"] = "E = mc^2" },
                new JsonObject { ["text"] = @"x = \frac{-b \pm \sqrt{b^2 - 5ac}" },
                new JsonObject { ["text"] = "2a" }),
        }.ToJsonString();
        using var reader = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(new Answer(read)));
        var result = await new VerifyToolkit(reader, root, null).Image(canvas, new Dictionary<string, object?> { ["name"] = "eq" });

        var items = ((object?[])result["items"]!).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal(["seen", "missed"], items.Select(i => (string)i["status"]!));
        Assert.Equal("E = mc^2", items[0]["readAs"]);
        var stray = Assert.Single(((object?[])result["strays"]!).Cast<Dictionary<string, object?>>());
        Assert.Equal(("different figure", "x = (−b ± √(b² − 4ac))/(2a)"), ((string)stray["kind"]!, (string)stray["near"]!));
        Assert.Equal("x = \\frac{-b \\pm \\sqrt{b^2 - 5ac}\n2a", stray["read"]);
    }

    /// <summary>
    /// End to end on a real canvas: one line read as set, one read with another figure, one painted over and so missed. The
    /// still goes to the reader as an image; the stray's crop is saved and it stays pending until settled.
    /// </summary>
    [Fact]
    public async Task TestAStillIsCheckedAndItsStraysWaitForTheirCrops()
    {
        var canvas = new global::Polson.Drawing.Skia.SkiaCanvas(400, 240);
        var ctx = canvas.GetContext();
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(0, 0, 400, 240);
        ctx.FillStyle = "#111111";
        ctx.Font = "28px Arial";
        ctx.FillText("BE COOL", 20, 50);
        ctx.FillText("Returned $974 million", 20, 110);
        ctx.FillText("+46%", 20, 170);
        ctx.FillText("Hidden note", 20, 220);
        ctx.FillRect(0, 195, 400, 45);                // painted over: the reader cannot see it
        ctx.FillText("Off the page", 900, 50);

        var read = new JsonObject
        {
            ["items"] = new JsonArray(
                new JsonObject { ["text"] = "BE COOL" },
                new JsonObject { ["text"] = "Returned $744 million" },
                new JsonObject { ["text"] = "+46%" }),
        }.ToJsonString();
        var answer = new Answer(read);
        using var reader = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(answer));
        var recorded = new List<string>();
        var session = new SessionContext();
        var verify = new VerifyToolkit(reader, root, session, (type, _) => recorded.Add(type));

        var result = await verify.Image(ctx, new Dictionary<string, object?> { ["computed"] = new Dictionary<string, object?> { ["+46%"] = "582.3 / 399.0 − 1" } });

        Assert.True((bool)result["ok"]!, result.GetValueOrDefault("error") as string);
        Assert.Contains("\"image_url\"", answer.LastRequest);
        Assert.True(File.Exists(Path.Combine(root, "artifacts", "verify", "image.png")));
        var items = ((object?[])result["items"]!).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal(["BE COOL", "Returned $974 million", "+46%", "Hidden note", "Off the page"], items.Select(i => (string)i["text"]!));
        Assert.Equal(["seen", "missed", "seen", "missed", "off the canvas"], items.Select(i => (string)i["status"]!));
        Assert.Equal(["brief", "document", "computed", "own words", "own words"], items.Select(i => (string)i["source"]!));
        var stray = Assert.Single(((object?[])result["strays"]!).Cast<Dictionary<string, object?>>());
        Assert.Equal(("different figure", "Returned $974 million", "artifacts/verify/image-stray-1.png"), ((string)stray["kind"]!, (string)stray["near"]!, (string)stray["frame"]!));
        Assert.True(File.Exists(Path.Combine(root, "artifacts", "verify", "image-stray-1.png")));
        Assert.False((bool)result["passed"]!);
        Assert.Contains("look at its crop", (string)result["summary"]!);

        // A citation passed as computed is set aside: its working shows no arithmetic.
        var cited = await new VerifyToolkit(reader, root, null).Image(canvas, new Dictionary<string, object?> { ["computed"] = new Dictionary<string, object?> { ["+46%"] = "from the letter" } });
        Assert.Contains("1 computed entry was not used", (string)cited["summary"]!);
        Assert.Equal("unsourced figure", ((object?[])cited["items"]!).Cast<Dictionary<string, object?>>().Single(i => (string)i["text"]! == "+46%")["source"]);

        var refused = Assert.Throws<InvalidOperationException>(() => verify.Stray(1, "misread"));
        Assert.Contains("artifacts/verify/image-stray-1.png", refused.Message);
        var later = new VerifyToolkit(reader, root, session, (type, _) => recorded.Add(type));
        var settled = later.Stray(1, "misread", "the crop shows $974 million");
        Assert.True((bool)settled["passed"]!);
        Assert.Equal(["image.verify", "check", "image.stray", "check"], recorded);

        // The same pixels checked again keep the verdict already given; the check passes without settling again.
        var again = await new VerifyToolkit(reader, root, session).Image(canvas, new Dictionary<string, object?> { ["computed"] = new Dictionary<string, object?> { ["+46%"] = "582.3 / 399.0 − 1" } });
        Assert.True((bool)again["passed"]!, (string)again["summary"]!);

        Assert.Throws<ArgumentException>(() => verify.Image(canvas, new Dictionary<string, object?> { ["name"] = "../x" }).GetAwaiter().GetResult());
        Assert.Throws<ArgumentException>(() => verify.Image("artifacts/final.png").GetAwaiter().GetResult());
    }

    /// <summary>
    /// becool3's credits had no document behind them, and `computed` refused them as no arithmetic, so they fell to own words.
    /// A fact stated in `sources` is checked on the web instead: supported, unsettled, or not supported, which fails the
    /// check. A text the brief holds keeps the brief as its source, and is not sent.
    /// </summary>
    [Fact]
    public async Task TestAStatedFactIsCheckedOnTheWebAndOnlyWhereNothingElseSourcesIt()
    {
        var canvas = new global::Polson.Drawing.Skia.SkiaCanvas(600, 260);
        var ctx = canvas.GetContext();
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(0, 0, 600, 260);
        ctx.FillStyle = "#111111";
        ctx.Font = "24px Arial";
        string[] lines = ["BE COOL", "2005 · DIRECTED BY F. GARY GRAY", "DIRECTED BY BARRY SONNENFELD", "RUNS 118 MINUTES"];
        for (var i = 0; i < lines.Length; i++) ctx.FillText(lines[i], 20, 50 + 55 * i);
        var read = new JsonObject { ["items"] = new JsonArray([.. lines.Select(l => (JsonNode)new JsonObject { ["text"] = l })]) }.ToJsonString();
        using var reader = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(new Answer(read)));
        var web = new Decisions(claim => claim.Contains("Sonnenfeld") ? 0.017 : claim.Contains("118") ? 0.456 : 0.999);
        using var parallel = new global::Polson.ExtendedMind.ParallelSearch.ParallelClient("k", new HttpClient(web));
        var facts = new global::Polson.ExtendedMind.ParallelSearch.FactChecker(parallel);
        Dictionary<string, object?> Fact(string claim) => new() { ["about"] = "Be Cool (2005 film)", ["claim"] = claim };
        var options = new Dictionary<string, object?>
        {
            ["sources"] = new Dictionary<string, object?>
            {
                ["BE COOL"] = Fact("Be Cool is a 2005 film."),
                ["2005 · DIRECTED BY F. GARY GRAY"] = Fact("Be Cool (2005) was directed by F. Gary Gray."),
                ["DIRECTED BY BARRY SONNENFELD"] = Fact("Be Cool (2005) was directed by Barry Sonnenfeld."),
                ["RUNS 118 MINUTES"] = Fact("Be Cool (2005) runs 118 minutes."),
            },
        };

        var result = await new VerifyToolkit(reader, root, null, facts: facts).Image(canvas, options);

        Assert.True((bool)result["ok"]!, result.GetValueOrDefault("error") as string);
        var items = ((object?[])result["items"]!).Cast<Dictionary<string, object?>>().ToDictionary(i => (string)i["text"]!);
        Assert.Equal("brief", items["BE COOL"]["source"]);                                    // the brief outranks the web
        Assert.Equal(3, web.Claims.Count);
        Assert.DoesNotContain(web.Claims, c => c.Contains("is a 2005 film"));
        Assert.Equal(["web", "web", "web"], lines[1..].Select(l => (string)items[l]["source"]!));
        Assert.Equal(["supported", "not supported", "unsettled"], lines[1..].Select(l => (string)((Dictionary<string, object?>)items[l]["web"]!)["verdict"]!));
        Assert.StartsWith("supported (0.999)", (string)items[lines[1]]["detail"]!);
        Assert.False((bool)result["passed"]!);
        var summary = (string)result["summary"]!;
        Assert.Contains("1 stated fact not supported by the sources the web check read ('DIRECTED BY BARRY SONNENFELD')", summary);
        Assert.Contains("1 stated fact the sources leave unsettled ('RUNS 118 MINUTES')", summary);
        Assert.DoesNotContain("no source", summary);                                           // 2005 is sourced by the stated fact

        // With nothing to check them, they are recorded as unchecked, and that alone does not fail the check.
        var bare = await new VerifyToolkit(reader, root, null).Image(canvas, new Dictionary<string, object?>
        {
            ["sources"] = new Dictionary<string, object?> { ["2005 · DIRECTED BY F. GARY GRAY"] = Fact("Be Cool (2005) was directed by F. Gary Gray.") },
        });
        Assert.Contains("1 stated fact not checked on the web (no ApiKeys:Parallel is configured)", (string)bare["summary"]!);

        var malformed = await Assert.ThrowsAsync<ArgumentException>(() => new VerifyToolkit(reader, root, null).Image(canvas,
            new Dictionary<string, object?> { ["sources"] = new Dictionary<string, object?> { ["BE COOL"] = "a 2005 film" } }));
        Assert.Contains("{ about, claim }", malformed.Message);
    }

    /// <summary>A billing line names several people: each is its own claim, and one wrong name fails the line.</summary>
    [Fact]
    public async Task TestABillingLineIsCheckedNameByNameAndFailsOnItsWeakest()
    {
        var canvas = new global::Polson.Drawing.Skia.SkiaCanvas(800, 120);
        var ctx = canvas.GetContext();
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(0, 0, 800, 120);
        ctx.FillStyle = "#111111";
        ctx.Font = "24px Arial";
        const string billing = "JOHN TRAVOLTA · UMA THURMAN · RENE RUSSO";
        const string director = "2005 · DIRECTED BY F. GARY GRAY";
        ctx.FillText(billing, 20, 50);
        ctx.FillText(director, 20, 100);
        // becool5: the year is in the brief, which sourced the whole line by its figure and left the name unchecked.
        File.WriteAllText(Path.Combine(root, "brief.md"), "# Brief\n<!-- BRIEF-BEGIN -->\nA trailer for Be Cool (2005).\n<!-- BRIEF-END -->\n");
        var read = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["text"] = billing }, new JsonObject { ["text"] = director }) }.ToJsonString();
        using var reader = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(new Answer(read)));
        var web = new Decisions(claim => claim.Contains("Russo") ? 0.02 : 0.999);
        using var parallel = new global::Polson.ExtendedMind.ParallelSearch.ParallelClient("k", new HttpClient(web));
        var facts = new global::Polson.ExtendedMind.ParallelSearch.FactChecker(parallel);

        var result = await new VerifyToolkit(reader, root, null, facts: facts).Image(canvas, new Dictionary<string, object?>
        {
            ["sources"] = new Dictionary<string, object?>
            {
                [billing] = new Dictionary<string, object?>
                {
                    ["about"] = "Be Cool (2005 film)",
                    ["claim"] = new object[] { "John Travolta stars in Be Cool (2005).", "Uma Thurman stars in Be Cool (2005).", "Rene Russo stars in Be Cool (2005)." },
                },
                [director] = new Dictionary<string, object?> { ["about"] = "Be Cool (2005 film)", ["claim"] = "Be Cool (2005) was directed by F. Gary Gray." },
            },
        });

        Assert.Equal(4, web.Claims.Count);
        var items = ((object?[])result["items"]!).Cast<Dictionary<string, object?>>().ToDictionary(i => (string)i["text"]!);
        Assert.Equal("brief", items[director]["source"]);                                     // the year keeps its source
        Assert.Equal("brief.md; web: supported (0.999): en.wikipedia.org", items[director]["detail"]);   // and the name is checked
        Assert.Equal("supported", ((Dictionary<string, object?>)items[director]["web"]!)["verdict"]);
        var item = items[billing];
        var check = (Dictionary<string, object?>)item["web"]!;
        Assert.Equal("not supported", check["verdict"]);
        Assert.Equal(3, ((object?[])check["checks"]!).Length);
        Assert.StartsWith("3 claims, the weakest not supported (0.020)", (string)item["detail"]!);
        Assert.Contains("Rene Russo", (string)item["detail"]!);
        Assert.False((bool)result["passed"]!);
    }
    #endregion

    #region Types
    /// <summary>The watcher: answers every request with one transcription, as a chat completion.</summary>
    /// <summary>A worked integral as an aligned derivation is written: every line follows, and the check records itself.</summary>
    [Fact]
    public void TestADerivationThatHoldsPassesAndRecordsItsVerdict()
    {
        var recorded = new List<(string Type, IReadOnlyDictionary<string, object?> Fields)>();
        var verify = new VerifyToolkit(null, root, null, (t, f) => recorded.Add((t, f)));
        var result = verify.Derivation(new object[]
        {
            @"\int_0^2 \frac{x^2}{2}\,dx", @"&= \left[\frac{x^3}{6}\right]_0^2 \\", @"&= \frac{8}{6} \\", @"&= \frac{4}{3}",
        }, new Dictionary<string, object?> { ["name"] = "area under x²/2" });

        Assert.True((bool)result["passed"]!, (string)result["summary"]!);
        Assert.Equal(["∫₀² x²/2 dx", "[x³/6]₀²", "8/6", "4/3"], ((object?[])result["steps"]!).Cast<string>());
        Assert.Equal(3, ((Dictionary<string, object?>)result["counts"]!)["proved"]);
        Assert.Equal(["derivation.verify", "check"], recorded.Select(r => r.Type));
        var check = recorded[1].Fields;
        Assert.Equal(("Derivation area under x²/2: every step follows from the last", true), ((string)check["claim"]!, (bool)check["passed"]!));
    }

    /// <summary>A wrong step fails the derivation, named with its counterexample; a solution's steps are equations.</summary>
    [Fact]
    public void TestAWrongStepFailsTheDerivationAndSolvingIsChecked()
    {
        var verify = new VerifyToolkit(null, root, null);
        var wrong = verify.Derivation(new object[] { "(a+b)^2", "a^2 + b^2", "a^2 + b^2" });
        Assert.False((bool)wrong["passed"]!);
        var first = (Dictionary<string, object?>)((object?[])wrong["pairs"]!)[0]!;
        Assert.Equal(("differs", 1, 2), ((string)first["verdict"]!, (int)first["from"]!, (int)first["to"]!));
        Assert.Contains("Step 1 → 2 differs", (string)wrong["summary"]!);

        Assert.True((bool)verify.Derivation(new object[] { "2x + 3 = 7", "2x = 4", "x = 2" })["passed"]!);
        var lost = verify.Derivation(new object[] { "x^2 = 4", "x = 2" });                 // a root lost is not a step
        Assert.False((bool)lost["passed"]!);
        Assert.Contains("not equivalent", (string)((Dictionary<string, object?>)((object?[])lost["pairs"]!)[0]!)["detail"]!);
        Assert.Throws<ArgumentException>(() => verify.Derivation(new object[] { "x" }));
        Assert.Throws<ArgumentException>(() => verify.Derivation(new object[] { "x", "x" }, new Dictionary<string, object?> { ["steps"] = 2 }));
    }

    /// <summary>
    /// ftcexplainer1 wrote an equation followed by expressions, and was told an equation and an expression do not compare.
    /// Such a step is a chain, checked as its sides: the equation's own claim is checked, so a wrong antiderivative with
    /// right arithmetic after it fails. A sum's subscript = is not a side, and equations among equations stay a solution.
    /// </summary>
    [Fact]
    public void TestAnEquationBesideAnExpressionIsCheckedAsAChain()
    {
        var verify = new VerifyToolkit(null, root, null);
        var right = verify.Derivation(new object[] { @"\int_0^2 x^2\,dx = \left[\frac{x^3}{3}\right]_0^2", @"\frac{2^3}{3} - \frac{0^3}{3}", @"\frac{8}{3}" });
        Assert.True((bool)right["passed"]!, (string)right["summary"]!);
        Assert.Equal(["∫₀² x² dx", "[x³/3]₀²", "2³/3 − 0³/3", "8/3"], ((object?[])right["steps"]!).Cast<string>());
        Assert.Equal(3, ((object?[])right["given"]!).Length);
        var pairs = ((object?[])right["pairs"]!).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal([(1, 1), (1, 2), (2, 3)], pairs.Select(p => ((int)p["from"]!, (int)p["to"]!)));
        Assert.Contains("4 expressions with each equation's sides apart", (string)right["summary"]!);

        var wrong = verify.Derivation(new object[] { @"\int_0^2 x^2\,dx = \left[\frac{x^4}{4}\right]_0^2", @"\frac{2^4}{4} - 0", "4" });
        Assert.False((bool)wrong["passed"]!);
        Assert.Contains("Step 1's sides differs", (string)wrong["summary"]!);

        Assert.True((bool)verify.Derivation(new object[] { @"\sum_{k=1}^{n} k = \frac{n(n+1)}{2}", @"\frac{n^2 + n}{2}" },
            new Dictionary<string, object?> { ["integers"] = new[] { "n" } })["passed"]!);
        Assert.True((bool)verify.Derivation(new object[] { "(x+1)^2 = x^2 + 2x + 1 = 1 + 2x + x^2", "x^2 + 2x + 1" })["passed"]!);
        Assert.Equal(["2x + 3 = 7", "2x = 4", "x = 2"], ((object?[])verify.Derivation(new object[] { "2x + 3 = 7", "2x = 4", "x = 2" })["steps"]!).Cast<string>());
        Assert.Equal([@"\sum_{k=1}^{n} k", @"\frac{n(n+1)}{2}"], VerifyToolkit.Sides(@"\sum_{k=1}^{n} k = \frac{n(n+1)}{2}"));
        Assert.Equal([@"\{x : x = 1\}"], VerifyToolkit.Sides(@"\{x : x = 1\}"));

        // ftcexplainer3: a chain of equations, each beginning where the last ended. Each is an identity, which holds on
        // its own; as steps of a solution their lhs − rhs are all 0, and every pair was unsettled.
        var identities = verify.Derivation(new object[]
        {
            @"\int_0^2 x^2\,dx = \left[\frac{x^3}{3}\right]_0^2", @"\left[\frac{x^3}{3}\right]_0^2 = \frac{2^3}{3} - \frac{0^3}{3}",
            @"\frac{2^3}{3} - \frac{0^3}{3} = \frac{8}{3}",
        });
        Assert.True((bool)identities["passed"]!, (string)identities["summary"]!);
        Assert.Contains("identity", (string)((Dictionary<string, object?>)((object?[])identities["pairs"]!)[0]!)["detail"]!);
        Assert.True((bool)verify.Derivation(new object[] { @"A = \frac{x^3}{3}", @"\frac{d}{dx}\left(\frac{x^3}{3}\right) = x^2" })["passed"]!);
        var falseStep = verify.Derivation(new object[] { @"\left[\frac{x^3}{3}\right]_0^2 = \frac{2^3}{3}", @"\frac{2^3}{3} = 3" });
        Assert.False((bool)falseStep["passed"]!);
        Assert.Contains("different numbers", (string)falseStep["summary"]!);
        Assert.False((bool)verify.Derivation(new object[] { "x^2 = 4", "x = 2" })["passed"]!);     // still a lost root, not an identity
        Assert.Equal(["a", "b", "c"], VerifyToolkit.Sides("a = b = c"));
        Assert.Equal(["x = "], VerifyToolkit.Sides("x = "));
    }

    /// <summary>A comp.equation handle is a step; and the claim a derivation records cannot be written by Stage.check.</summary>
    [Fact]
    public void TestEquationHandlesAreStepsAndTheClaimIsReserved()
    {
        var comp = new MotionToolkit(root).Composition(new Hashtable { ["width"] = 320d, ["height"] = 180d });
        var steps = new object[]
        {
            comp.Equation(new Hashtable { ["latex"] = @"\frac{d}{dx} x^3" }),
            comp.Equation(new Hashtable { ["latex"] = "3x^2" }),
        };
        Assert.True((bool)new VerifyToolkit(null, root, null).Derivation(steps)["passed"]!);

        var result = new JsDrawingEngine().Execute("const c = createCanvas(4, 4); Stage.check('Derivation mine: every step follows from the last', true); c;", 4, 4, null, "png", 90);
        Assert.False(result.Success);
        Assert.Contains("Verify.derivation", result.Error);
    }

    /// <summary>
    /// The steps as the reader read them are checked too: a misreading that breaks the chain is reported against the
    /// reading, and the derivation itself still passes.
    /// </summary>
    [Fact]
    public async Task TestTheStepsAsReadAreCheckedBesideTheStepsAsSet()
    {
        var canvas = new global::Polson.Drawing.Skia.SkiaCanvas(900, 400);
        var ctx = canvas.GetContext();
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(0, 0, 900, 400);
        ctx.FillStyle = "#111111";
        string[] steps = [@"\frac{8}{6}", @"\frac{4}{3}", @"1.\overline{3}"];
        ctx.FillEquation(steps[0], 40, 100, new Hashtable { ["size"] = 48f });
        ctx.FillEquation(steps[1], 40, 200, new Hashtable { ["size"] = 48f });
        var read = new JsonObject
        {
            ["items"] = new JsonArray(new JsonObject { ["text"] = @"$\frac{8}{6}$" }, new JsonObject { ["text"] = @"\frac{3}{4}" }),
        }.ToJsonString();
        using var reader = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(5), root, http: new HttpClient(new Answer(read)));
        var verify = new VerifyToolkit(reader, root, null);
        await verify.Image(canvas, new Dictionary<string, object?> { ["name"] = "steps" });

        var result = verify.Derivation(new object[] { steps[0], steps[1] });
        Assert.True((bool)result["passed"]!, (string)result["summary"]!);
        var pair = (Dictionary<string, object?>)((object?[])result["pairs"]!)[0]!;
        var asRead = Assert.IsType<Dictionary<string, object?>>(pair["read"]);
        Assert.Equal(("8/6", "3/4", "differs"), ((string)asRead["a"]!, (string)asRead["b"]!, (string)asRead["verdict"]!));
        Assert.Contains("As the watcher read them", (string)result["summary"]!);
    }

    private (VerifyToolkit Verify, Answer Http, List<(string Type, IReadOnlyDictionary<string, object?> Fields)> Recorded) Claimant(string answer)
    {
        var http = new Answer(answer);
        var reader = new VideoReader([new NemotronEndpoint(NemotronEndpoint.Nebius, "https://tf.example/v1", "k", NemotronEndpoint.NebiusModel)],
            new DocumentBudget(20), root, http: new HttpClient(http));
        var recorded = new List<(string, IReadOnlyDictionary<string, object?>)>();
        return (new VerifyToolkit(reader, root, null, (t, f) => recorded.Add((t, f))), http, recorded);
    }

    private static string Js(string code) => "```javascript\n" + code + "\n```";

    /// <summary>
    /// A claim is checked by the code the model wrote, run in the engine: the verdict is the code's, the code is returned
    /// and recorded, and the claim goes to the model as text, with no media part.
    /// </summary>
    [Fact]
    public async Task TestAClaimIsCheckedByTheCodeTheModelWrote()
    {
        var (verify, http, recorded) = Claimant(Js("const pct = (582.3 - 399.0) / 399.0 * 100;\ncheck('up 46%', rounds(pct, 46), { pct });"));
        var r = await verify.Claim("Revenue grew from $399.0M to $582.3M, up 46%.", new Dictionary<string, object?> { ["name"] = "growth" });

        Assert.True((bool)r["passed"]!, (string)r["summary"]!);
        Assert.Equal("holds", r["verdict"]);
        var check = (Dictionary<string, object?>)Assert.Single((object?[])r["checks"]!)!;
        Assert.Contains("45.93", (string)check["detail"]!);
        Assert.Contains("rounds(pct, 46)", (string)r["code"]!);
        Assert.Contains("Claim: Revenue grew from $399.0M", http.LastRequest);
        Assert.DoesNotContain("image_url", http.LastRequest);
        Assert.DoesNotContain("video_url", http.LastRequest);
        Assert.Equal(["claim.verify", "check"], recorded.Select(e => e.Type));
        Assert.Equal(("Claim checked: growth", true), ((string)recorded[1].Fields["claim"]!, (bool)recorded[1].Fields["passed"]!));
    }

    /// <summary>A failing check fails the claim, with what decided it; the routines' results serialise into the detail.</summary>
    [Fact]
    public async Task TestAClaimThatDoesNotHoldFailsWithItsEvidence()
    {
        var (verify, _, _) = Claimant(Js("""
            const r = Equation.equivalent('(a + b)^2', 'a^2 + b^2');
            check('(a + b)^2 = a^2 + b^2', r.verdict === 'proved' || r.verdict === 'agrees', r);
            const d = Verify.derivation(['2x + 3 = 7', '2x = 4', 'x = 2']);
            check('the worked solution', d.passed, d.summary);
            """));
        var r = await verify.Claim("The square of a sum is the sum of the squares, and 2x + 3 = 7 gives x = 2.");
        Assert.Equal(("fails", false), ((string)r["verdict"]!, (bool)r["passed"]!));
        var checks = ((object?[])r["checks"]!).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal([false, true], checks.Select(c => (bool)c["passed"]!));
        Assert.Contains("\"verdict\":\"differs\"", (string)checks[0]["detail"]!);
        Assert.Contains("(a + b)^2 = a^2 + b^2", (string)r["summary"]!);
    }

    /// <summary>
    /// The code runs with nothing to misuse: no Assets or the rest of Verify, no eval, no endless loop; each is an error
    /// verdict, not a pass. A check passed by a literal true is refused without running; an answer with no code checks nothing.
    /// </summary>
    [Theory]
    [InlineData("const m = Assets.material('stone'); check('x', m !== null, 'm');", "error")]
    [InlineData("const v = Verify.film; check('x', v !== undefined, 'v');", "error")]
    [InlineData("check('x', eval('1 + 1') === 2, 'eval');", "error")]
    [InlineData("let n = 0; while (true) { n++; } check('x', n > 0, 'loop');", "error")]
    [InlineData("check(\"the claim holds\", true, 'because I say so');", "refused")]
    [InlineData("const x = 1 + 1;", "unchecked")]
    public async Task TestTheSandboxHoldsAndAVerdictMustBeComputed(string code, string verdict)
    {
        var (verify, _, _) = Claimant(Js(code));
        var r = await verify.Claim($"A claim checked by: {code}", new Dictionary<string, object?> { ["budget"] = 2000 });
        Assert.Equal((verdict, false), ((string)r["verdict"]!, (bool)r["passed"]!));
        Assert.Equal("unchecked", (await Claimant("I think it is true.").Verify.Claim("Prose, no code."))["verdict"]);
    }

    /// <summary>
    /// integrate and solve answer what the CAS cannot settle by sampling: a definite integral to an infinite limit, a root
    /// in a bracket. A wrong figure fails; no sign change is null, not a root; a divergent integrand is NaN, not a pass.
    /// </summary>
    [Fact]
    public async Task TestIntegrateAndSolveCheckAnIntegralAndARoot()
    {
        var (verify, _, _) = Claimant(Js("""
            const g = integrate(x => Math.exp(-x * x), -Infinity, Infinity);
            check('the Gaussian integral is sqrt(pi)', close(g, Math.sqrt(Math.PI), 1e-8), { g });
            check('the area under sin over [0, pi] is 2', close(integrate(Math.sin, 0, Math.PI), 2), 'sin');
            const r = solve(x => x * x - 2, 0, 2);
            check('x^2 = 2 has the root 1.41421', r !== null && rounds(r, 1.41421), { r });
            check('the area under x^2 over [0, 1] is 1/2', close(integrate(x => x * x, 0, 1), 0.5), 'x^2');
            check('x^2 + 1 has a root in [-3, 3]', solve(x => x * x + 1, -3, 3) !== null, 'none');
            check('1/x is integrable over [0, 1]', !isNaN(integrate(x => 1 / x, 0, 1)), '1/x');
            check('1/sqrt(x) over [0, 1] is 2', close(integrate(x => 1 / Math.sqrt(x), 0, 1), 2, 1e-6), 'improper');
            check('e^-x over [0, Infinity) is 1', close(integrate(x => Math.exp(-x), 0, Infinity), 1, 1e-8), 'tail');
            """));
        var r = await verify.Claim("Integrals and roots.");
        var checks = ((object?[])r["checks"]!).Cast<Dictionary<string, object?>>().ToArray();
        Assert.Equal([true, true, true, false, false, false, true, true], checks.Select(c => (bool)c["passed"]!));
        Assert.Equal("fails", (string)r["verdict"]!);

        var (bad, _, _) = Claimant(Js("check('x', solve(1, 0, 2) !== null, 'f');"));
        Assert.Equal("error", (string)(await bad.Claim("solve needs a function."))["verdict"]!);
        var (loop, _, _) = Claimant(Js("check('x', integrate(x => { while (true) {} }, 0, 1) > 0, 'loop');"));
        Assert.Equal("error", (string)(await loop.Claim("An endless integrand.", new Dictionary<string, object?> { ["budget"] = 2000 }))["verdict"]!);
    }

    [Fact]
    public void TestRoundsAtTheStatedPrecisionAndCloseToATolerance()
    {
        Assert.True(VerifyToolkit.Rounds(45.9398, 46, Jint.Native.JsValue.Undefined));
        Assert.True(VerifyToolkit.Rounds(4.4072, 4.4, Jint.Native.JsValue.Undefined));
        Assert.False(VerifyToolkit.Rounds(20.83, 18, Jint.Native.JsValue.Undefined));
        Assert.False(VerifyToolkit.Rounds(4.46, 4.4, Jint.Native.JsValue.Undefined));
        Assert.True(VerifyToolkit.Rounds(33.1, 33, new Jint.Native.JsNumber(0)));
        Assert.True(VerifyToolkit.Close(0.331, 1.1 * 1.1 * 1.1 - 1, Jint.Native.JsValue.Undefined));
        Assert.False(VerifyToolkit.Close(0.2, 0.1487, Jint.Native.JsValue.Undefined));

        var result = new JsDrawingEngine().Execute("const c = createCanvas(4, 4); Stage.check('Claim checked: mine', true); c;", 4, 4, null, "png", 90);
        Assert.False(result.Success);
        Assert.Contains("Verify.claim", result.Error);
    }

    private sealed class Answer(string content) : HttpMessageHandler
    {
        /// <summary>The last request's body, to see how the piece was sent.</summary>
        public string LastRequest { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new JsonObject
                {
                    ["choices"] = new JsonArray(new JsonObject { ["message"] = new JsonObject { ["content"] = content }, ["finish_reason"] = "stop" }),
                    ["usage"] = new JsonObject { ["total_tokens"] = 100 },
                }.ToJsonString()),
            };
        }
    }

    /// <summary>Parallel's Decisions API: each claim answered with the probability <c>score</c> gives it, citing Wikipedia.</summary>
    private sealed class Decisions(Func<string, double> score) : HttpMessageHandler
    {
        public List<string> Claims { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
            var claim = (string)body["questions"]!["claim"]!["instructions"]!;
            Claims.Add(claim);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new JsonObject
                {
                    ["answers"] = new JsonObject { ["claim"] = new JsonObject { ["type"] = "noul", ["noul"] = score(claim) } },
                    ["citations"] = new JsonArray(new JsonObject { ["url"] = "https://en.wikipedia.org/wiki/Be_Cool", ["excerpts"] = new JsonArray() }),
                    ["usage"] = new JsonObject { ["input_tokens"] = 4000, ["output_tokens"] = 0 },
                    ["decision_id"] = "d",
                }.ToJsonString()),
            };
        }
    }
    #endregion
}
