namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Executes every runnable example published in <c>docs/Polson.core.md</c>. The SDK reference is the
/// primary — and often the only — view an agent has of the API, so an example that no longer runs is a
/// functional defect rather than a documentation blemish.
/// <para>
/// <see cref="ApiDocumentationTests"/> already checks the reference's member <i>names</i> against
/// reflection in both directions. That catches a call that vanished; it cannot catch a call whose
/// arguments, return shape or surrounding idiom changed underneath an example that still spells the
/// name correctly. The manuals have had this treatment since <see cref="ManualExampleTests"/>; the
/// reference the manuals cite did not.
/// </para>
/// <para>
/// Fence convention, the same one the manuals keep: a <c>```javascript</c> fence at column 0 is a
/// complete program an agent can paste into <c>ExecuteScript</c> unchanged, and is executed here. A
/// fragment is tagged <c>```js</c> — a two-line illustration of a signature, a passage assuming a
/// <c>ctx</c> from further up the page, or the second half of a two-script sequence that reads what
/// the first left in <c>Session</c>. An example inside a callout is quoted, so it sits off column 0
/// and is an illustration by construction.
/// </para>
/// </summary>
public class CoreReferenceExampleTests : TestsRuntime
{
    #region Properties
    /// <summary>Each example paired with the line it starts on, so a failure names a place in the file.</summary>
    public static TheoryData<int, string> Examples
    {
        get
        {
            var data = new TheoryData<int, string>();
            foreach (var (line, script) in JavaScriptBlocks(Core)) data.Add(line, script);
            return data;
        }
    }
    #endregion

    #region Tests
    /// <remarks>
    /// Rendered rather than run with <c>render: false</c>. The measurement that justifies suppressing
    /// a render — an encode costing several times the script — does not apply to nine small examples,
    /// and rasterising is the half where a paper that serialises to unusable SVG would show up. This
    /// runs the whole path an agent's own <c>ExecuteScript</c> call takes.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Examples))]
    public void TestCoreReferenceExampleExecutes(int line, string script)
    {
        // A throwaway project root, so an example that writes an artifact (Motion.sheet) is provably
        // contained somewhere other than the repository it is documenting.
        var root = Directory.CreateTempSubdirectory("polson-core-doc-").FullName;
        try
        {
            var engine = new JsDrawingEngine { ProjectRoot = root };
            var result = engine.Execute(script, 900, 700);

            Assert.True(result.Success,
                $"docs/Polson.core.md:{line} example failed: {result.Error}\n--- script ---\n{script}");

            // Not every example draws — one declares a stage, one lists fonts, one requisitions and
            // exits — so bytes are asserted only where a surface was actually produced.
            Assert.True(result.ImageBytes is null or { Length: > 0 },
                $"docs/Polson.core.md:{line} example rendered to zero bytes.");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// The reference must keep publishing pasteable programs. Without a floor, demoting every example
    /// to <c>```js</c> would turn this file into a test that passes by having no cases.
    /// </summary>
    [Fact]
    public void TestCoreReferencePublishesRunnableExamples()
    {
        Assert.True(JavaScriptBlocks(Core).Count >= 9,
            "docs/Polson.core.md publishes fewer runnable ```javascript examples than it used to. " +
            "Demoting a fragment to ```js is fine and the floor moves with it; the reference no longer " +
            "shipping complete programs is not.");
    }
    #endregion

    #region Methods
    /// <summary>The reference as the server serves it, placeholders substituted — what an agent reads.</summary>
    private static string Core { get; } = PolsonResources.Docs.Core();

    private static List<(int Line, string Script)> JavaScriptBlocks(string markdown) =>
        [.. Fence.Matches(markdown)
            .Select(m => (Line: LineOf(markdown, m.Index), Script: m.Groups[1].Value.Trim()))
            .Where(b => b.Script.Length > 0)];

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>Shared verbatim with <see cref="ManualExampleTests"/>, so both docs keep one convention.</summary>
    private static readonly Regex Fence = new(
        @"^```javascript\r?\n(.*?)^```",
        RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
    #endregion
}
