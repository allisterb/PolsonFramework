namespace Polson.Tests.ExtendedMind;

using System;
using System.IO;
using System.Threading.Tasks;

using global::Polson.Tests;
using Polson.ExtendedMind.DocumentProcessing;
using SkiaSharp;
using Xunit;

/// <summary>
/// <c>Documents.ask</c> against the live model, on a PDF this test builds.
/// </summary>
/// <remarks>
/// <para>
/// Gated behind <c>POLSON_LIVE_DOCUMENT_TESTS=1</c> because it bills, and skipped silently when no
/// key is configured. Everything decidable offline is covered by <see cref="DocumentProcessorTests"/>;
/// what only a live call can prove is that a PDF sent as inline bytes is actually <i>read</i> — which
/// is the one assumption the whole design rests on and the one no fake can check.
/// </para>
/// <para>
/// <b>The figures are invented, and that is the point.</b> A test using real box-office data would
/// pass whether the model read the document or answered from what it already knew, so it would prove
/// nothing about document processing at all. Nothing outside this file has ever heard of Northwind
/// Studios, so a correct answer can only have come from the bytes.
/// </para>
/// </remarks>
public class DocumentLiveTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-doclive-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public DocumentLiveTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests
    /// <summary>A figure that exists only in the PDF comes back, with provenance and a clean scan.</summary>
    [Fact]
    public async Task TestAFigureIsReadOutOfASuppliedPdf()
    {
        var key = config["ApiKeys:GoogleAgentPlatform"];
        if (string.IsNullOrWhiteSpace(key)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_DOCUMENT_TESTS") != "1") return;

        WriteLedger(Path.Combine(root, "boxoffice.pdf"));

        var budget = new DocumentBudget(4);
        using var processor = new DocumentProcessor(key, budget, root);

        var answer = await processor.Ask("boxoffice.pdf",
            "Which film had the highest total gross, and what was that total? "
            + "Answer with the title and the number only.");

        Assert.True(answer.Success, answer.Error);

        // The film with the highest total in the ledger, and its figure. Neither is knowable without
        // reading the file — and "Cold Lantern" is not the first row, so a model skimming the top
        // would get it wrong.
        Assert.Contains("Cold Lantern", answer.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("140.5", answer.Text, StringComparison.Ordinal);

        Assert.Empty(answer.Warnings);
        Assert.Equal(1, budget.Spent);

        var provenance = Assert.IsType<DocumentProvenance>(answer.Provenance);
        Assert.Equal("application/pdf", provenance.MimeType);
        Assert.Equal("boxoffice.pdf", provenance.Source);
        Assert.True(provenance.Bytes > 0);
        Assert.True(provenance.TokensSpent > 0, "the service should report what the read cost");
        Assert.Equal(16, provenance.Hash.Length);
    }
    #endregion

    #region Methods
    /// <summary>
    /// A one-page ledger of films that do not exist.
    /// </summary>
    /// <remarks>
    /// Built with Skia rather than shipped as a fixture so the expected answers live beside the
    /// assertions: a checked-in binary would drift from the test that reads it, and nobody would
    /// notice until the assertion failed for the wrong reason.
    /// </remarks>
    private static void WriteLedger(string path)
    {
        using var stream = new SKFileWStream(path);
        using var document = SKDocument.CreatePdf(stream);

        var canvas = document.BeginPage(400, 300);
        using var font = new SKFont(SKTypeface.Default, 13);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        string[] lines =
        [
            "NORTHWIND STUDIOS - 2031 RELEASES",
            "",
            "Film                 Opening    Total",
            "The Glass Harbour      12.4      88.1",
            "Nine Sparrows           7.9      41.6",
            "Cold Lantern           22.3     140.5",
            "Errand of Salt          3.1      19.8",
            "",
            "Figures in USD millions.",
        ];

        var y = 40f;
        foreach (var line in lines)
        {
            canvas.DrawText(line, 24, y, SKTextAlign.Left, font, paint);
            y += 22;
        }

        document.EndPage();
        document.Close();
    }
    #endregion
}
