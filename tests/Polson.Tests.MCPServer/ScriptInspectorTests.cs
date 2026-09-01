namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Asking a script what it declares, without reading it.
/// </summary>
/// <remarks>
/// The case this is built for is real and measured. A live <c>comic_studio</c> Critic read twenty-four
/// <c>.js</c> files whole — three of them twice — to answer questions like <i>is <c>SHAFT</c> actually
/// drawn?</i>, pulling 917 KB of tool results into its window to do it. It got the right answer; it
/// just had to carry the whole program.
/// <para>
/// The tests that matter most are the ones about <b>not</b> matching: a text search answers "is SHAFT
/// used?" with hits inside <c>SHAFT_TOP</c> and inside a comment, and a tool that did the same would
/// be a slower <c>grep</c>. These are resolved identifiers, and the tests say so.
/// </para>
/// </remarks>
public class ScriptInspectorTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-ast-" + Guid.NewGuid().ToString("N"));

    /// <summary>A script shaped the way the workflows ask for one: constants, then a layer per function.</summary>
    private const string Artwork = """
        // The Penciler's measurements, named once.
        const ANCHORS = { SHAFT: { rx: 343, y: 812 }, DECK: { y: 800 } };
        const SHAFT_TOP = 812;          // deliberately similar to SHAFT
        const PALETTE = { ink: '#101014' };

        function drawShaft(ctx) {
            // SHAFT is mentioned here in a comment, and used below.
            ctx.fillStyle = PALETTE.ink;
            ctx.fillRect(0, ANCHORS.SHAFT.y, ANCHORS.SHAFT.rx, 40);
        }

        function drawDeck(ctx) {
            ctx.fillRect(0, ANCHORS.DECK.y, 100, 10);
        }

        const canvas = createCanvas(1024, 1408);
        drawShaft(canvas.getContext('2d'));
        canvas;
        """;
    #endregion

    #region Methods
    public ScriptInspectorTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private DrawingMcpTools Tools() => new(null, null, null, root);

    private string Write(string name, string body)
    {
        File.WriteAllText(Path.Combine(root, name), body);
        return name;
    }

    private static string[] Names(JsonObject outline) =>
        [.. outline["declarations"]!.AsArray().Select(d => d!["name"]!.GetValue<string>())];
    #endregion

    #region Outline
    [Fact]
    public void TestTheOutlineNamesEveryTopLevelDeclaration()
    {
        Write("artwork.js", Artwork);

        var outline = Tools().InspectScript("artwork.js");

        Assert.Equal(
            ["ANCHORS", "SHAFT_TOP", "PALETTE", "drawShaft", "drawDeck", "canvas"],
            Names(outline));
    }

    [Fact]
    public void TestTheOutlineSaysWhatKindEachDeclarationIs()
    {
        Write("artwork.js", Artwork);

        var byName = Tools().InspectScript("artwork.js")["declarations"]!.AsArray()
            .ToDictionary(d => d!["name"]!.GetValue<string>(), d => d!["kind"]!.GetValue<string>());

        Assert.Equal("const", byName["ANCHORS"]);
        Assert.Equal("function", byName["drawShaft"]);
    }

    /// <summary>
    /// The outline's size follows the number of declarations, not the size of their bodies.
    /// </summary>
    /// <remarks>
    /// The saving is not "smaller than the file" — on a short script the JSON can easily be longer,
    /// and asserting otherwise would be a claim that fails on exactly the files nobody needs this
    /// for. It is that the answer stays flat while the file grows: the real scripts are 20–50 KB
    /// because their function *bodies* are long, and none of that reaches the outline.
    /// </remarks>
    [Fact]
    public void TestTheOutlineDoesNotGrowWithTheBodiesItSummarises()
    {
        // Sized like the scripts this is for: the run that motivated it emitted twenty between
        // 20 and 50 KB, all of them long because their function bodies were.
        var padding = string.Concat(Enumerable.Repeat("    ctx.fillRect(0, 0, 1, 1);\n", 1500));
        var fat = Artwork.Replace("ctx.fillStyle = PALETTE.ink;", padding);

        Write("small.js", Artwork);
        Write("fat.js", fat);

        var small = Tools().InspectScript("small.js").ToJsonString().Length;
        var large = Tools().InspectScript("fat.js").ToJsonString().Length;

        Assert.True(fat.Length > Artwork.Length * 10, "the fixture must actually be much larger");

        // The one declaration that grew reports a bigger span; nothing else changes.
        Assert.True(large < small * 1.2,
            $"outline grew from {small} to {large} while the file grew {Artwork.Length} -> {fat.Length}");
        Assert.True(large < fat.Length / 20,
            "the outline of a large script must be a small fraction of it");
    }

    /// <summary>Line and character spans, because a surgical edit will splice by range.</summary>
    [Fact]
    public void TestEachDeclarationCarriesItsSpan()
    {
        Write("artwork.js", Artwork);

        var shaft = Tools().InspectScript("artwork.js")["declarations"]!.AsArray()
            .Single(d => d!["name"]!.GetValue<string>() == "drawShaft")!;

        var start = shaft["start"]!.GetValue<int>();
        var end = shaft["end"]!.GetValue<int>();

        Assert.True(start < end);
        Assert.StartsWith("function drawShaft", Artwork[start..end], StringComparison.Ordinal);
        Assert.True(shaft["endLine"]!.GetValue<int>() > shaft["line"]!.GetValue<int>());
    }
    #endregion

    #region Finding one thing
    [Fact]
    public void TestFindLocatesADeclarationAndItsUses()
    {
        Write("artwork.js", Artwork);

        var found = Tools().InspectScript("artwork.js", name: "ANCHORS");

        Assert.True(found["found"]!.GetValue<bool>());
        Assert.Equal("const", found["declaration"]!["kind"]!.GetValue<string>());

        // Twice in drawShaft, once in drawDeck. Its own declaration is never counted, or every
        // unused constant would report as used once.
        Assert.Equal(3, found["referenceCount"]!.GetValue<int>());
    }

    /// <summary>
    /// A name is not a substring, and not a word in a comment.
    /// </summary>
    /// <remarks>
    /// This is what distinguishes the tool from a search. `grep SHAFT` over this file reports the
    /// declaration, the `SHAFT_TOP` constant, the comment, and the real uses, all as equals.
    /// </remarks>
    [Fact]
    public void TestReferencesAreIdentifiersRatherThanTextMatches()
    {
        Write("artwork.js", Artwork);

        // `SHAFT` here is only ever a property of ANCHORS — never a bare identifier — so the
        // similarly-named constant and the comment must not be reported as uses of it.
        var shaftTop = Tools().InspectScript("artwork.js", name: "SHAFT_TOP");

        Assert.True(shaftTop["found"]!.GetValue<bool>());
        Assert.Equal(0, shaftTop["referenceCount"]!.GetValue<int>());
    }

    /// <summary>A declared-but-unused constant is exactly what an audit wants to find.</summary>
    [Fact]
    public void TestAnUnusedDeclarationReportsNoReferences()
    {
        Write("artwork.js", Artwork);

        var deck = Tools().InspectScript("artwork.js", name: "drawDeck");

        Assert.True(deck["found"]!.GetValue<bool>());
        Assert.Equal(0, deck["referenceCount"]!.GetValue<int>());
    }

    [Fact]
    public void TestTheSourceOfOneDeclarationCanBeAskedFor()
    {
        Write("artwork.js", Artwork);

        var found = Tools().InspectScript("artwork.js", name: "drawShaft", includeSource: true);
        var source = found["source"]!.GetValue<string>();

        Assert.StartsWith("function drawShaft", source, StringComparison.Ordinal);
        Assert.Contains("fillRect", source, StringComparison.Ordinal);

        // One function, not the file.
        Assert.DoesNotContain("drawDeck", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// "No such declaration" is an answer, and is phrased as one.
    /// </summary>
    /// <remarks>
    /// An agent that reads an empty result as "the lookup failed" goes and reads the whole file to
    /// check, which is the cost this exists to remove. So the reply says it is definitive and lists
    /// what is declared, which is what the caller needed next anyway.
    /// </remarks>
    [Fact]
    public void TestAMissingNameIsADefiniteAnswerNotAnEmptyResult()
    {
        Write("artwork.js", Artwork);

        var found = Tools().InspectScript("artwork.js", name: "drawGallery");

        Assert.False(found["found"]!.GetValue<bool>());
        Assert.Contains("No top-level declaration", found["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("drawShaft", found["declared"]!.AsArray().Select(n => n!.GetValue<string>()));
    }
    #endregion

    #region Failures
    /// <summary>
    /// Unparseable JavaScript is reported with a position, because the engine would reject it too.
    /// </summary>
    /// <remarks>
    /// Acornima is Jint's own parser, so what fails here fails there. That makes this a cheap way to
    /// find a syntax error without spending an execution on it.
    /// </remarks>
    [Fact]
    public void TestABrokenScriptSaysWhereItBroke()
    {
        Write("broken.js", "const A = { x: 1;\nfunction drawShaft(ctx) {}\n");

        var ex = Assert.Throws<ArgumentException>(() => Tools().InspectScript("broken.js"));

        Assert.Contains("broken.js", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestAMissingFileIsReportedInTheCallersTerms()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => Tools().InspectScript("artwork.js"));

        Assert.Contains("artwork.js", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The same containment every other path parameter gets.</summary>
    [Theory]
    [InlineData("../escape.js")]
    [InlineData("../../escape.js")]
    public void TestAPathOutsideTheProjectIsRefused(string attempt)
    {
        var outside = Path.Combine(Path.GetDirectoryName(root)!, "escape.js");
        File.WriteAllText(outside, Artwork);

        try
        {
            Assert.ThrowsAny<Exception>(() => Tools().InspectScript(attempt));
        }
        finally
        {
            File.Delete(outside);
        }
    }
    #endregion

    #region The record
    /// <summary>
    /// Asking about a file is looking at it, and the record should say so.
    /// </summary>
    /// <remarks>
    /// `artifact.read` is the one direct evidence in the record that a pass coordinated with an
    /// earlier one through the environment rather than through its own context. A structural question
    /// is that same act, and if it were silent then making the tool cheaper would make the record
    /// emptier — the studio would look less collaborative the better it got.
    /// </remarks>
    [Fact]
    public void TestInspectingAScriptIsRecordedAsAReadAndNotAsSilence()
    {
        Write("artwork.js", Artwork);
        Tools().InspectScript("artwork.js", name: "ANCHORS");

        var events = Path.Combine(root, "events", "server.jsonl");
        Assert.True(File.Exists(events), "inspecting a script recorded nothing at all");
        Assert.Contains("artifact.read", File.ReadAllText(events), StringComparison.Ordinal);
    }
    #endregion
}
