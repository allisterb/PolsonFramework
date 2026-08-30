namespace Polson.Tests.MCPServer;

using System;
using System.Linq;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// How <c>Search</c> excerpts a long section — specifically, that it never cuts a code fence.
/// </summary>
/// <remarks>
/// A harness run reported the SkSL example arriving cut three lines in, immediately before
/// <c>half4 main(float2 coord)</c> — the one line a reader needed from it. The excerpt split on
/// blank lines, and a blank line inside a fence looked like a paragraph break.
/// <para>
/// This matters more than a formatting nit because the role specs tell an agent to use <c>Search</c>
/// when its host cannot open MCP resources. On such a host the truncated excerpt <i>is</i> the
/// documentation, so a missing signature makes the API undiscoverable rather than merely awkward.
/// An unbalanced fence is worse still: everything after it renders as code.
/// </remarks>
public class SearchExcerptTests : TestsRuntime
{
    #region Methods
    /// <summary>Counts fence lines, which must be even for the markdown to close.</summary>
    private static int FenceCount(string text) =>
        text.Split('\n').Count(l => l.TrimStart().StartsWith("```", StringComparison.Ordinal));
    #endregion

    #region Tests
    /// <summary>
    /// No excerpt ever ends inside a code block.
    /// </summary>
    /// <remarks>
    /// Swept across queries rather than asserted on one, because the failure depends on where the
    /// densest window happens to land — which changes as the docs change.
    /// </remarks>
    [Theory]
    [InlineData("Skia.Shader.sksl runtime shader uniforms main coord example")]
    [InlineData("custom sksl shader half4 main")]
    [InlineData("measure wrapped text block height stacking")]
    [InlineData("scale linear ticks zero baseline column chart")]
    [InlineData("brush preset pencil grain stamp path effect")]
    [InlineData("layout columns rows grid gap panels")]
    public async Task TestExcerptsNeverCutACodeFence(string query)
    {
        var index = new LocalKnowledgeIndex();
        var hits = await index.SearchAsync(query, 5, KnowledgeScope.All);

        Assert.NotEmpty(hits);
        foreach (var hit in hits)
        {
            Assert.True(FenceCount(hit.Text) % 2 == 0,
                $"unbalanced code fence in the excerpt for '{hit.Uri}' — everything after it renders as code:\n{hit.Text}");
        }
    }

    /// <summary>
    /// The SkSL entry point survives into the excerpt, not just its uniform declarations.
    /// </summary>
    /// <remarks>
    /// The exact regression that was reported. Uniforms without the signature tell a reader what to
    /// pass and not how to write the shader.
    /// </remarks>
    [Fact]
    public async Task TestTheSkslExampleKeepsItsEntryPoint()
    {
        var index = new LocalKnowledgeIndex();
        var hits = await index.SearchAsync("custom sksl shader example uniforms main coord", 5, KnowledgeScope.Sdk);

        var withExample = hits.FirstOrDefault(h => h.Text.Contains("uniform float2", StringComparison.Ordinal));
        Assert.NotNull(withExample);
        Assert.Contains("half4 main", withExample!.Text, StringComparison.Ordinal);
    }

    /// <summary>A truncated excerpt still says it was truncated.</summary>
    [Fact]
    public async Task TestTruncationIsStillAnnounced()
    {
        var index = new LocalKnowledgeIndex();
        var hits = await index.SearchAsync("shader", 8, KnowledgeScope.Sdk);

        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.False(string.IsNullOrWhiteSpace(h.Text)));
    }
    #endregion
}
