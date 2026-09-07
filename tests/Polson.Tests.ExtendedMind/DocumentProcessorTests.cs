namespace Polson.Tests.ExtendedMind;

using System;
using System.IO;
using System.Threading.Tasks;

using global::Polson.Tests;
using Polson.ExtendedMind.DocumentProcessing;
using Xunit;

/// <summary>
/// Reading a supplied document — everything decidable without a network.
/// </summary>
/// <remarks>
/// <para>
/// The success path needs a live model, so what is proven here is the half that decides whether a
/// call is even attempted: containment, type declaration, size, budget, and the shape of every
/// refusal. That is the half worth pinning, because <b>each of these is checked before the budget is
/// touched</b> and a regression would show up as spend rather than as a failing test.
/// </para>
/// <para>
/// The order is deliberate and is asserted: a bad path costs nothing even when the budget is empty,
/// because a caller fixing two problems should not have to fix them one refusal at a time.
/// </para>
/// </remarks>
public class DocumentProcessorTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-docs-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public DocumentProcessorTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Configuration Tests
    /// <summary>
    /// No key is a normal configuration: the surface still exists and refuses in words.
    /// </summary>
    /// <remarks>
    /// The failure being avoided is a <c>ReferenceError</c> on <c>Documents</c>, which an agent
    /// cannot interpret and would route around by answering from recall.
    /// </remarks>
    [Fact]
    public async Task AnUnconfiguredProcessorRefusesWithARemedy()
    {
        var processor = new DocumentProcessor(null, new DocumentBudget(5), root);

        Assert.False(processor.IsAvailable);

        var answer = await processor.Ask("a.pdf", "what is in it");

        Assert.False(answer.Success);
        Assert.Equal("NotConfigured", answer.FailureName);
        Assert.Contains("not configured", answer.Remedy, StringComparison.OrdinalIgnoreCase);
        Assert.False(answer.Retryable);
    }
    #endregion

    #region Containment Tests
    /// <summary>
    /// A document is read from inside the project directory or not at all.
    /// </summary>
    /// <remarks>
    /// The same containment <c>outFile</c> and <c>Skia.Image.load</c> enforce, and it matters more
    /// here: this call's whole purpose is to send file contents to a third party, so an escape would
    /// exfiltrate rather than merely misplace.
    /// </remarks>
    [Theory]
    [InlineData("../escaped.pdf")]
    [InlineData("../../etc/passwd")]
    public async Task APathOutsideTheProjectIsRefused(string path)
    {
        var processor = new DocumentProcessor("k", new DocumentBudget(5), root);

        var answer = await processor.Ask(path, "read it");

        Assert.False(answer.Success);
        Assert.Equal("NotFound", answer.FailureName);
        Assert.Contains("outside this project", answer.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingFileSaysSoRatherThanBeingSent()
    {
        var processor = new DocumentProcessor("k", new DocumentBudget(5), root);

        var answer = await processor.Ask("nope.pdf", "read it");

        Assert.False(answer.Success);
        Assert.Equal("NotFound", answer.FailureName);
        Assert.Contains("relative to the project directory", answer.Remedy, StringComparison.Ordinal);
    }
    #endregion

    #region Listing Tests
    /// <summary>
    /// Listing is free, offline, and works with no key — it is how an agent sees what it has.
    /// </summary>
    /// <remarks>
    /// Unmetered on purpose. A surface where finding out what exists costs the same as reading it
    /// pushes an agent into guessing file names, which is the behaviour this replaces.
    /// </remarks>
    [Fact]
    public void ListFindsDocumentsWithNoKeyAndNoBudget()
    {
        Directory.CreateDirectory(Path.Combine(root, "documents"));
        File.WriteAllText(Path.Combine(root, "documents", "returns.csv"), "a,b\n1,2\n");

        var processor = new DocumentProcessor(null, new DocumentBudget(0), root);
        var found = processor.List();

        var entry = Assert.Single(found);
        Assert.Equal("documents/returns.csv", entry.Path);
        Assert.Equal("returns.csv", entry.Name);
        Assert.Equal("text/csv", entry.MimeType);
        Assert.True(entry.Bytes > 0);
    }

    /// <summary>A path is returned ready to hand straight back to <c>ask</c>.</summary>
    /// <remarks>
    /// Forward slashes and project-relative, so a nested document does not need repair on Windows —
    /// where <c>GetRelativePath</c> would otherwise hand back a backslashed path that reads as an
    /// escape sequence the moment it is put in a JS string.
    /// </remarks>
    [Fact]
    public void AListedPathIsUsableAsGiven()
    {
        Directory.CreateDirectory(Path.Combine(root, "documents", "2025"));
        File.WriteAllText(Path.Combine(root, "documents", "2025", "q4.csv"), "a\n1\n");

        var entry = Assert.Single(new DocumentProcessor(null, new DocumentBudget(0), root).List());

        Assert.Equal("documents/2025/q4.csv", entry.Path);
        Assert.DoesNotContain('\\', entry.Path);
    }

    /// <summary>The folder's own README is machinery, not source material.</summary>
    [Fact]
    public void ListOmitsTheFoldersOwnReadme()
    {
        Directory.CreateDirectory(Path.Combine(root, "documents"));
        File.WriteAllText(Path.Combine(root, "documents", "README.md"), "# documents");

        Assert.Empty(new DocumentProcessor(null, new DocumentBudget(0), root).List());
    }

    /// <summary>A type the surface cannot declare is omitted rather than listed then refused.</summary>
    [Fact]
    public void ListOmitsWhatCouldNotBeRead()
    {
        Directory.CreateDirectory(Path.Combine(root, "documents"));
        File.WriteAllText(Path.Combine(root, "documents", "notes.docx"), "x");
        File.WriteAllText(Path.Combine(root, "documents", "notes.txt"), "x");

        var entry = Assert.Single(new DocumentProcessor(null, new DocumentBudget(0), root).List());
        Assert.Equal("documents/notes.txt", entry.Path);
    }

    [Fact]
    public void ListIsEmptyWhenThereIsNoFolder() =>
        Assert.Empty(new DocumentProcessor(null, new DocumentBudget(0), root).List());

    /// <summary>
    /// A wrong path is told what the right ones are.
    /// </summary>
    /// <remarks>
    /// An agent one character out — <c>boxoffice.pdf</c> for <c>box-office-2025.pdf</c> — otherwise
    /// spends a turn per guess. The same move <c>peek</c> makes when it lists the artifacts folder.
    /// </remarks>
    [Fact]
    public async Task AWrongPathNamesWhatIsActuallyThere()
    {
        Directory.CreateDirectory(Path.Combine(root, "documents"));
        File.WriteAllText(Path.Combine(root, "documents", "box-office-2025.csv"), "a\n1\n");

        var answer = await new DocumentProcessor("k", new DocumentBudget(3), root)
            .Ask("boxoffice.csv", "read it");

        Assert.Equal("NotFound", answer.FailureName);
        Assert.Contains("documents/box-office-2025.csv", answer.Error!, StringComparison.Ordinal);
    }

    /// <summary>With nothing staged, the message says so rather than listing an empty set.</summary>
    [Fact]
    public async Task AWrongPathWithNoDocumentsSaysThereAreNone()
    {
        var answer = await new DocumentProcessor("k", new DocumentBudget(3), root)
            .Ask("anything.pdf", "read it");

        Assert.Equal("NotFound", answer.FailureName);
        Assert.Contains("no documents at all", answer.Error!, StringComparison.Ordinal);
    }
    #endregion

    #region Cache Tests
    /// <summary>
    /// The same question of the same document costs once.
    /// </summary>
    /// <remarks>
    /// The loop this shortens is re-running a script, which is how an agent works: a drawing is
    /// built by editing <c>artwork.js</c> and running it again, and a <c>Documents.ask</c> near the
    /// top of that file was re-billed on every pass.
    /// </remarks>
    [Fact]
    public async Task ARepeatedReadIsServedFromCacheAndCostsNothing()
    {
        var budget = new DocumentBudget(5);
        var processor = new FakeProcessor(budget, root, new DocumentCache(Path.Combine(root, "cache")));
        File.WriteAllText(Path.Combine(root, "a.txt"), "hello");

        var first = await processor.Ask("a.txt", "what does it say");
        var second = await processor.Ask("a.txt", "what does it say");

        Assert.True(second.Success, second.Error);
        Assert.Equal(first.Text, second.Text);
        Assert.Equal(1, processor.Calls);        // the service was reached once
        Assert.Equal(1, budget.Spent);           // and charged once
        Assert.Equal(1, budget.CacheHits);
        Assert.True(second.Provenance!.FromCache);
        Assert.False(first.Provenance!.FromCache);
    }

    /// <summary>
    /// A hit is served even when the allowance is spent, because it costs nothing.
    /// </summary>
    /// <remarks>
    /// The ordering assertion: checking affordability before the cache would refuse a read that was
    /// free, which is the same class of mistake as charging for one.
    /// </remarks>
    [Fact]
    public async Task ACacheHitIsServedAfterTheBudgetIsExhausted()
    {
        var budget = new DocumentBudget(1);
        var processor = new FakeProcessor(budget, root, new DocumentCache(Path.Combine(root, "cache")));
        File.WriteAllText(Path.Combine(root, "a.txt"), "hello");

        await processor.Ask("a.txt", "what does it say");
        Assert.Equal(0, budget.Remaining);

        var again = await processor.Ask("a.txt", "what does it say");

        Assert.True(again.Success, again.Error);
        Assert.True(again.Provenance!.FromCache);
    }

    /// <summary>
    /// A different question of the same document is a different read.
    /// </summary>
    /// <remarks>
    /// The failure this prevents was live in the Northwind run: it asked for a field extraction and
    /// then for a verbatim transcription of the same PDF. Keyed on the document alone, the second
    /// call would have been served the first call's answer — the right shape of text for the wrong
    /// question, with nothing downstream able to tell.
    /// </remarks>
    [Fact]
    public async Task ADifferentQuestionIsNotACacheHit()
    {
        var budget = new DocumentBudget(5);
        var processor = new FakeProcessor(budget, root, new DocumentCache(Path.Combine(root, "cache")));
        File.WriteAllText(Path.Combine(root, "a.txt"), "hello");

        await processor.Ask("a.txt", "list every film");
        await processor.Ask("a.txt", "transcribe it verbatim");

        Assert.Equal(2, processor.Calls);
        Assert.Equal(2, budget.Spent);
        Assert.Equal(0, budget.CacheHits);
    }

    /// <summary>A different document is a different read, even for the same question.</summary>
    [Fact]
    public async Task ADifferentDocumentIsNotACacheHit()
    {
        var budget = new DocumentBudget(5);
        var processor = new FakeProcessor(budget, root, new DocumentCache(Path.Combine(root, "cache")));
        File.WriteAllText(Path.Combine(root, "a.txt"), "hello");
        File.WriteAllText(Path.Combine(root, "b.txt"), "different content entirely");

        await processor.Ask("a.txt", "what does it say");
        await processor.Ask("b.txt", "what does it say");

        Assert.Equal(2, processor.Calls);
        Assert.Equal(0, budget.CacheHits);
    }

    /// <summary>A different model is a different read.</summary>
    /// <remarks>Serving a cheaper model's answer to a call that asked for a better one is a lie about provenance.</remarks>
    [Fact]
    public async Task ADifferentModelIsNotACacheHit()
    {
        var budget = new DocumentBudget(5);
        var processor = new FakeProcessor(budget, root, new DocumentCache(Path.Combine(root, "cache")));
        File.WriteAllText(Path.Combine(root, "a.txt"), "hello");

        await processor.Ask("a.txt", "what does it say");
        await processor.Ask("a.txt", "what does it say", new DocumentOptions { Model = "gemini-2.5-pro" });

        Assert.Equal(2, processor.Calls);
        Assert.Equal(0, budget.CacheHits);
    }

    /// <summary>A hit reports the original read's cost and time, not this call's.</summary>
    /// <remarks>
    /// Stamping a hit with "now" would erase how old the answer being used is — and a cached answer
    /// to a document that has since changed is the one way this surface can be quietly stale.
    /// </remarks>
    [Fact]
    public async Task AHitCarriesTheOriginalReadsProvenance()
    {
        var processor = new FakeProcessor(new DocumentBudget(5), root, new DocumentCache(Path.Combine(root, "cache")));
        File.WriteAllText(Path.Combine(root, "a.txt"), "hello");

        var first = await processor.Ask("a.txt", "what does it say");
        var second = await processor.Ask("a.txt", "what does it say");

        Assert.Equal(first.Provenance!.ReadUtc, second.Provenance!.ReadUtc);
        Assert.Equal(first.Provenance.TokensSpent, second.Provenance.TokensSpent);
    }

    /// <summary>An unreadable entry misses rather than delivering nothing as an answer.</summary>
    /// <remarks>A cache is an optimisation and must never be the reason a read fails.</remarks>
    [Fact]
    public async Task ACorruptedEntryIsTreatedAsAMiss()
    {
        var dir = Path.Combine(root, "cache");
        Directory.CreateDirectory(dir);
        var key = DocumentCache.KeyOf("doc", "query", "model", "text/plain");

        // Stands in for a half-written or hand-edited file; nothing legitimate writes this.
        File.WriteAllText(Path.Combine(dir, key + ".json"), "{ not json");

        Assert.Null(await new DocumentCache(dir).Get(key));
    }

    /// <summary>An entry holding no text is not an answer, and misses.</summary>
    [Fact]
    public async Task AnEmptyEntryIsTreatedAsAMiss()
    {
        var dir = Path.Combine(root, "cache-empty");
        var cache = new DocumentCache(dir);
        var key = DocumentCache.KeyOf("doc", "query", "model", "text/plain");

        await cache.Put(key, new CachedAnswer { Text = "  ", Model = "m", ReadUtc = DateTime.UtcNow });

        Assert.Null(await cache.Get(key));
    }

    /// <summary>Every part of the key changes the address.</summary>
    [Fact]
    public void TheKeyCoversDocumentQueryModelAndType()
    {
        var baseline = DocumentCache.KeyOf("doc", "query", "model", "text/plain");

        Assert.NotEqual(baseline, DocumentCache.KeyOf("other", "query", "model", "text/plain"));
        Assert.NotEqual(baseline, DocumentCache.KeyOf("doc", "other", "model", "text/plain"));
        Assert.NotEqual(baseline, DocumentCache.KeyOf("doc", "query", "other", "text/plain"));
        Assert.NotEqual(baseline, DocumentCache.KeyOf("doc", "query", "model", "application/pdf"));
        Assert.Equal(baseline, DocumentCache.KeyOf("doc", "query", "model", "text/plain"));
    }
    #endregion

    #region Type Tests
    /// <summary>
    /// An extension with no known type is refused rather than guessed at.
    /// </summary>
    /// <remarks>
    /// Guessing is the failure that matters: declaring the wrong type produces a confident answer
    /// about nothing, and nothing downstream can tell that from a correct one.
    /// </remarks>
    [Fact]
    public async Task AnUnknownExtensionIsRefusedByName()
    {
        var processor = new DocumentProcessor("k", new DocumentBudget(5), root);
        File.WriteAllText(Path.Combine(root, "records.xyz"), "data");

        var answer = await processor.Ask("records.xyz", "read it");

        Assert.False(answer.Success);
        Assert.Equal("UnsupportedType", answer.FailureName);
        Assert.Contains(".pdf", answer.Error!, StringComparison.Ordinal);
    }

    /// <summary>Bytes carry no name, so they need an explicit type.</summary>
    [Fact]
    public async Task BytesWithoutAMimeTypeAreRefused()
    {
        var processor = new DocumentProcessor("k", new DocumentBudget(5), root);

        var answer = await processor.Ask(new byte[] { 1, 2, 3 }, "read it");

        Assert.False(answer.Success);
        Assert.Equal("UnsupportedType", answer.FailureName);
        Assert.Contains("mimeType", answer.Error!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Anything that is neither a path nor bytes is refused by type name.</summary>
    [Fact]
    public async Task AnUnusableSourceIsRefusedByTypeName()
    {
        var processor = new DocumentProcessor("k", new DocumentBudget(5), root);

        var answer = await processor.Ask(42, "read it");

        Assert.False(answer.Success);
        Assert.Equal("InvalidRequest", answer.FailureName);
        Assert.Contains("Int32", answer.Error!, StringComparison.Ordinal);
    }
    #endregion

    #region Query Tests
    /// <summary>A read with no question is refused before anything is opened.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyQueryIsRefused(string? query)
    {
        var processor = new DocumentProcessor("k", new DocumentBudget(5), root);

        var answer = await processor.Ask("a.pdf", query);

        Assert.False(answer.Success);
        Assert.Equal("NoQuery", answer.FailureName);
    }
    #endregion

    #region Budget Tests
    /// <summary>An exhausted budget refuses, and says not to invent the figure instead.</summary>
    [Fact]
    public async Task AnExhaustedBudgetRefuses()
    {
        var budget = new DocumentBudget(0);
        var processor = new DocumentProcessor("k", budget, root);
        File.WriteAllText(Path.Combine(root, "a.txt"), "hello");

        var answer = await processor.Ask("a.txt", "read it");

        Assert.False(answer.Success);
        Assert.Equal("BudgetExhausted", answer.FailureName);
        Assert.Contains("plausible number", answer.Remedy, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing locally refusable is charged, even when the budget is empty.
    /// </summary>
    /// <remarks>
    /// The ordering assertion. A bad path checked *after* the budget would report "budget exhausted"
    /// for a typo, sending the caller to fix the wrong thing — and a bad path checked after the spend
    /// would bill for a call never made.
    /// </remarks>
    [Fact]
    public async Task ALocalRefusalCostsNothingEvenWithNoBudget()
    {
        var budget = new DocumentBudget(0);
        var processor = new DocumentProcessor("k", budget, root);

        var answer = await processor.Ask("../escaped.pdf", "read it");

        Assert.Equal("NotFound", answer.FailureName);   // not BudgetExhausted
        Assert.Equal(0, budget.Spent);
    }

    [Fact]
    public void ABudgetReportsWhatIsLeft()
    {
        var budget = new DocumentBudget(3);

        Assert.Equal(3, budget.Remaining);
        Assert.True(budget.CanAfford(3));
        Assert.False(budget.CanAfford(4));

        // Spending is internal, exactly as it is on AssetBudget — a budget a caller could decrement
        // is not a budget. Zero total is therefore how an exhausted one is expressed from outside.
        Assert.Equal(0, new DocumentBudget(0).Remaining);
        Assert.False(new DocumentBudget(0).CanAfford());
    }
    #endregion

    #region Size Tests
    /// <summary>Too large is refused here rather than by a rejected request that already cost a call.</summary>
    [Fact]
    public async Task ADocumentOverTheInlineLimitIsRefusedBeforeSpending()
    {
        var budget = new DocumentBudget(5);
        var processor = new DocumentProcessor("k", budget, root);

        var answer = await processor.Ask(new byte[Documents.MaxInlineBytes + 1], "read it",
            new DocumentOptions { MimeType = "application/pdf" });

        Assert.False(answer.Success);
        Assert.Equal("TooLarge", answer.FailureName);
        Assert.Equal(0, budget.Spent);
    }
    #endregion

    #region Failure Shape Tests
    /// <summary>Only transport faults are worth repeating, and the flag says which.</summary>
    [Theory]
    [InlineData(DocumentFailure.RateLimited, true)]
    [InlineData(DocumentFailure.Network, true)]
    [InlineData(DocumentFailure.Timeout, true)]
    [InlineData(DocumentFailure.ServiceError, true)]
    [InlineData(DocumentFailure.NotFound, false)]
    [InlineData(DocumentFailure.UnsupportedType, false)]
    [InlineData(DocumentFailure.BudgetExhausted, false)]
    [InlineData(DocumentFailure.SafetyBlocked, false)]
    public void RetryableIsTrueOnlyForTransportFaults(DocumentFailure failure, bool expected) =>
        Assert.Equal(expected, new DocumentAnswer { Failure = failure }.Retryable);

    /// <summary>Every failure carries advice; a bare enum name is not a remedy.</summary>
    [Fact]
    public void EveryFailureHasARemedy()
    {
        foreach (DocumentFailure failure in Enum.GetValues<DocumentFailure>())
        {
            if (failure == DocumentFailure.None) continue;
            var remedy = new DocumentAnswer { Failure = failure }.Remedy;
            Assert.False(string.IsNullOrWhiteSpace(remedy), $"{failure} has no remedy");
            Assert.DoesNotContain("Unrecognised", remedy, StringComparison.Ordinal);
        }
    }

    /// <summary>A successful answer names no failure, so `!success` and a failure name agree.</summary>
    [Fact]
    public void ASuccessfulAnswerReportsNoFailure()
    {
        var answer = new DocumentAnswer { Success = true, Text = "42" };

        Assert.Equal("None", answer.FailureName);
        Assert.Equal(string.Empty, answer.Remedy);
        Assert.Empty(answer.Warnings);
    }
    #endregion

    #region Types
    /// <summary>
    /// A processor that answers without a network, so the surrounding behaviour can be tested.
    /// </summary>
    /// <remarks>
    /// It counts calls, which is what makes a cache assertion mean anything: a second read that
    /// merely returns equal text proves nothing, while a second read the service never sees is a hit.
    /// </remarks>
    private sealed class FakeProcessor(DocumentBudget budget, string? root, DocumentCache? cache)
        : DocumentProcessor("test-key", budget, root, "fake-model", cache)
    {
        public int Calls { get; private set; }

        protected override Task<(string? Text, long Tokens, string? Finish)> Generate(
            byte[] bytes, string mime, string query, string model, System.Threading.CancellationToken ct)
        {
            Calls += 1;
            return Task.FromResult<(string?, long, string?)>(($"answer to '{query}'", 100, null));
        }
    }
    #endregion
}
