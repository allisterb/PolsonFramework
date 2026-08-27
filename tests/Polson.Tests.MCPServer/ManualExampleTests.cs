namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Executes every runnable example published in the studio manuals. A manual that teaches a technique
/// with code an agent cannot run is worse than one that stays silent, so the examples are treated as
/// tested surface rather than prose.
/// <para>
/// Fence convention: <c>```javascript</c> marks a complete program an agent can paste into
/// <c>ExecuteScript</c> unchanged — those are executed here. <c>```js</c> marks an illustrative
/// fragment (a helper definition, a palette table, a shader body) that is not self-contained.
/// </para>
/// </summary>
public class ManualExampleTests : TestsRuntime
{
    #region Properties
    public static TheoryData<string, int, string> Examples
    {
        get
        {
            var data = new TheoryData<string, int, string>();
            foreach (var manual in PolsonManuals.All)
            {
                var blocks = JavaScriptBlocks(manual.Body);
                for (var i = 0; i < blocks.Count; i++)
                {
                    data.Add(manual.Id, i, blocks[i]);
                }
            }
            return data;
        }
    }
    #endregion

    #region Tests
    [Theory]
    [MemberData(nameof(Examples))]
    public void TestManualExampleExecutes(string manualId, int index, string script)
    {
        var engine = new JsDrawingEngine();
        var result = engine.Execute(script, 900, 700);

        Assert.True(result.Success,
            $"Manual {manualId} example #{index} failed: {result.Error}\n--- script ---\n{script}");
        Assert.NotNull(result.ImageBytes);
        Assert.NotEmpty(result.ImageBytes!);
    }

    /// <summary>
    /// Every manual calls the toolkit rather than describing it, and each must ship at least one
    /// end-to-end script an agent can paste into ExecuteScript unchanged.
    /// </summary>
    [Theory]
    [InlineData("01")]
    [InlineData("02")]
    [InlineData("03")]
    [InlineData("04")]
    [InlineData("05")]
    [InlineData("06")]
    [InlineData("07")]
    [InlineData("08")]
    [InlineData("09")]
    [InlineData("10")]
    [InlineData("11")]
    [InlineData("12")]
    public void TestEveryManualPublishesARunnableExample(string manualId)
    {
        var manual = PolsonManuals.Find(manualId);

        Assert.NotNull(manual);
        Assert.True(JavaScriptBlocks(manual!.Body).Count > 0,
            $"Manual {manualId} ({manual.Title}) publishes no runnable ```javascript example.");
    }
    #endregion

    #region Methods
    private static List<string> JavaScriptBlocks(string markdown) =>
        [.. Fence.Matches(markdown).Select(m => m.Groups[1].Value.Trim()).Where(s => s.Length > 0)];

    private static readonly Regex Fence = new(
        @"^```javascript\r?\n(.*?)^```",
        RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
    #endregion
}
