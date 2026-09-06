namespace Polson.Tests.MCPServer;

using System.Linq;

using Polson.MCPServer;

using Xunit;

/// <summary>
/// The check that fires when <c>outSvg</c> saves a document whose pictures a viewer will not see.
/// </summary>
/// <remarks>
/// This is the environment being edited rather than the trace being read — the vertical channel in
/// <c>CLAUDE.md</c> §2. The failure is undetectable from inside a run: the execution succeeds, the
/// SVG is written, and the photograph is simply absent wherever the deliverable is embedded as an
/// image. Warning at the moment of the write is the only place an agent is looking.
/// </remarks>
public class UnresolvedImageHrefTests
{
    #region Tests
    /// <summary>A data URI is exactly what the warning is asking for, so it is never reported.</summary>
    [Fact]
    public void TestADataUriIsNotReported() =>
        Assert.Empty(DrawingMcpTools.UnresolvableImageHrefs(
            """<svg><image x="0" y="0" width="10" height="10" href="data:image/png;base64,AAAA" /></svg>"""));

    /// <summary>
    /// The project-relative path is the case this exists for: it is what <c>outFile</c> and
    /// <c>Skia.Image.load</c> establish as the convention, so it is what an agent writes.
    /// </summary>
    [Fact]
    public void TestARelativePathIsReported()
    {
        var found = DrawingMcpTools.UnresolvableImageHrefs(
            """<svg><image href="artifacts/portrait.png" /></svg>""");

        Assert.Equal(["artifacts/portrait.png"], found);
    }

    /// <summary>An absolute URL fails the same way, and for the same reason.</summary>
    [Fact]
    public void TestAnAbsoluteUrlIsReported() =>
        Assert.Single(DrawingMcpTools.UnresolvableImageHrefs(
            """<svg><image href="https://upload.wikimedia.org/a/b.jpg" /></svg>"""));

    /// <summary>The SVG 1.1 spelling counts too — some tools still emit and expect it.</summary>
    [Fact]
    public void TestTheXlinkSpellingIsAlsoChecked() =>
        Assert.Single(DrawingMcpTools.UnresolvableImageHrefs(
            """<svg><image xlink:href="portrait.png" /></svg>"""));

    /// <summary>
    /// A mixed document reports only the broken half. A page that inlines four portraits and
    /// path-references a fifth is the realistic mistake, and the warning has to name the fifth.
    /// </summary>
    [Fact]
    public void TestOnlyTheUnresolvableOnesAreReported()
    {
        var found = DrawingMcpTools.UnresolvableImageHrefs("""
            <svg>
              <image href="data:image/jpeg;base64,AAAA" />
              <image href="data:image/jpeg;base64,BBBB" />
              <image href="artifacts/stage3.png" />
            </svg>
            """);

        Assert.Equal(["artifacts/stage3.png"], found);
    }

    /// <summary>
    /// An empty href draws nothing but is not an unresolved <i>reference</i>, so it is left to the
    /// author rather than reported as this particular mistake.
    /// </summary>
    [Fact]
    public void TestAnEmptyHrefIsNotReported() =>
        Assert.Empty(DrawingMcpTools.UnresolvableImageHrefs("""<svg><image href="" /></svg>"""));

    /// <summary>Other elements' hrefs are none of this check's business.</summary>
    [Fact]
    public void TestOnlyImageElementsAreExamined() =>
        Assert.Empty(DrawingMcpTools.UnresolvableImageHrefs(
            """<svg><use href="#mark" /><a href="https://example.org">x</a></svg>"""));

    /// <summary>
    /// Capped and deduplicated, because a generated page can carry hundreds of images and a warning
    /// longer than the response it rides on is a warning nobody reads.
    /// </summary>
    [Fact]
    public void TestReportingIsCappedAndDeduplicated()
    {
        var many = string.Concat(Enumerable.Range(0, 40).Select(i => $"""<image href="p{i}.png" />"""));
        var repeated = string.Concat(Enumerable.Repeat("""<image href="same.png" />""", 10));

        Assert.Equal(5, DrawingMcpTools.UnresolvableImageHrefs($"<svg>{many}</svg>").Count);
        Assert.Single(DrawingMcpTools.UnresolvableImageHrefs($"<svg>{repeated}</svg>"));
    }

    /// <summary>A very long href is truncated rather than pasted whole into the warning.</summary>
    [Fact]
    public void TestALongHrefIsTruncated()
    {
        var href = "https://example.org/" + new string('a', 300) + ".jpg";

        var reported = DrawingMcpTools.UnresolvableImageHrefs($"""<svg><image href="{href}" /></svg>""").Single();

        Assert.True(reported.Length <= 81, $"reported {reported.Length} chars");
        Assert.EndsWith("…", reported);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<svg></svg>")]
    [InlineData("not xml at all")]
    public void TestNothingToCheckIsNotAFailure(string xml) =>
        Assert.Empty(DrawingMcpTools.UnresolvableImageHrefs(xml));
    #endregion
}
