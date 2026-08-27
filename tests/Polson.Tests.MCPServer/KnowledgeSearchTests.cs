namespace Polson.Tests.MCPServer;

using System;
using System.Linq;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

public class KnowledgeSearchTests : TestsRuntime
{
    #region Manual Corpus Tests
    [Fact]
    public void TestAllManualsAreEmbeddedAndParsed()
    {
        var manuals = PolsonManuals.All;

        Assert.Equal(12, manuals.Count);
        Assert.Equal(["01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12"], manuals.Select(m => m.Id));
        Assert.All(manuals, m =>
        {
            Assert.NotEmpty(m.Title);
            Assert.NotEmpty(m.Purpose);
            Assert.NotEmpty(m.Body);
            Assert.Equal($"polson://manual/{m.Id}", m.Uri);
        });
    }

    [Fact]
    public void TestEveryManualBindsToRealSdkCalls()
    {
        var known = PolsonManuals.KnownSdkCalls();
        Assert.NotEmpty(known);

        foreach (var manual in PolsonManuals.All)
        {
            Assert.True(manual.Apis.Count > 0, $"Manual {manual.Id} ({manual.Title}) cites no SDK call — the theory is not bound to the API.");

            var unbound = manual.Apis.Where(a => !known.Contains(a)).ToArray();
            Assert.True(unbound.Length == 0,
                $"Manual {manual.Id} cites calls absent from the SDK core reference: {string.Join(", ", unbound)}");
        }
    }

    [Fact]
    public void TestManualResourcesAreAddressable()
    {
        var resources = PolsonManuals.ManualResources().ToList();

        Assert.Equal(PolsonManuals.All.Count, resources.Count);
        Assert.All(resources, r => Assert.StartsWith("polson://manual/", r.ProtocolResourceTemplate.UriTemplate));
    }

    [Fact]
    public void TestManualIndexListsEveryManualAndItsBindings()
    {
        var index = PolsonManuals.ManualIndex();

        Assert.All(PolsonManuals.All, m => Assert.Contains($"{m.Id} — {m.Title}", index));
        Assert.Contains("Drawing.createPerspectiveGrid", index);
        Assert.DoesNotContain("Cited but NOT in the SDK reference", index);
    }
    #endregion

    #region Corpus Chunking Tests
    [Fact]
    public void TestCorpusCoversBothManualAndSdkScopes()
    {
        var chunks = KnowledgeCorpus.Chunks;

        Assert.NotEmpty(chunks);
        Assert.Contains(chunks, c => c.Scope == KnowledgeScope.Manual);
        Assert.Contains(chunks, c => c.Scope == KnowledgeScope.Sdk);
        Assert.All(chunks, c => Assert.NotEmpty(c.Text));
        Assert.Equal(PolsonManuals.All.Count, chunks.Where(c => c.Scope == KnowledgeScope.Manual).Select(c => c.Uri).Distinct().Count());
    }
    #endregion

    #region Retrieval Ranking Tests
    [Theory]
    [InlineData("two point perspective vanishing point box", "polson://manual/06")]
    [InlineData("cast shadow umbra penumbra ground plane", "polson://manual/07")]
    [InlineData("eight head mannequin proportion pelvis", "polson://manual/08")]
    [InlineData("rule of thirds composition armature notan", "polson://manual/09")]
    [InlineData("golden ratio logo circles phi", "polson://manual/10")]
    [InlineData("loomis head construction ball and plane", "polson://manual/01")]
    public async Task TestSearchRanksTheRightManualFirst(string query, string expectedUri)
    {
        var index = new LocalKnowledgeIndex();
        var hits = await index.SearchAsync(query, 3, KnowledgeScope.Manual);

        Assert.NotEmpty(hits);
        Assert.Equal(expectedUri, hits[0].Uri);
    }

    [Fact]
    public async Task TestSearchSurfacesImplementingApis()
    {
        var index = new LocalKnowledgeIndex();
        var hits = await index.SearchAsync("construct a perspective cylinder with tangent ellipses", 5, KnowledgeScope.All);

        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.Apis.Contains("Drawing.drawPerspectiveCylinder"));
    }

    [Fact]
    public async Task TestSearchScopeFiltersCorpus()
    {
        var index = new LocalKnowledgeIndex();

        var manuals = await index.SearchAsync("perspective grid", 10, KnowledgeScope.Manual);
        var sdk = await index.SearchAsync("perspective grid", 10, KnowledgeScope.Sdk);

        Assert.NotEmpty(manuals);
        Assert.NotEmpty(sdk);
        Assert.All(manuals, h => Assert.Equal("manual", h.Source));
        Assert.All(sdk, h => Assert.Equal("sdk", h.Source));
    }

    [Fact]
    public async Task TestSearchMatchesApiNameByCamelCaseParts()
    {
        var index = new LocalKnowledgeIndex();
        var hits = await index.SearchAsync("createNotanPalette", 5, KnowledgeScope.All);

        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.Apis.Contains("Drawing.createNotanPalette"));
    }

    [Fact]
    public async Task TestSearchReturnsNothingForGibberishAndRespectsK()
    {
        var index = new LocalKnowledgeIndex();

        Assert.Empty(await index.SearchAsync("zzzqqqxxwv", 5, KnowledgeScope.All));
        Assert.True((await index.SearchAsync("perspective", 2, KnowledgeScope.All)).Count <= 2);
    }
    #endregion

    #region Search Tool Tests
    [Fact]
    public async Task TestSearchToolReturnsRankedEnvelope()
    {
        var tools = new DrawingMcpTools();
        var response = await tools.Search("volumetric sphere core shadow", 3, "manual");

        Assert.Equal("manual", response["scope"]!.GetValue<string>());
        Assert.Equal("local-bm25", response["backend"]!.GetValue<string>());
        Assert.True(response["count"]!.GetValue<int>() > 0);

        var first = response["results"]!.AsArray()[0]!;
        Assert.StartsWith("polson://", first["uri"]!.GetValue<string>());
        Assert.NotEmpty(first["text"]!.GetValue<string>());
    }
    #endregion
}
