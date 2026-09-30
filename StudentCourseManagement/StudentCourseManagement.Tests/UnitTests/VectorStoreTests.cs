using Microsoft.EntityFrameworkCore;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Infrastructure.Data;
using StudentCourseManagement.Infrastructure.Entities;
using StudentCourseManagement.Infrastructure.Repositories;
using StudentCourseManagement.Tests.TestDoubles;
using Xunit;

namespace StudentCourseManagement.Tests.UnitTests;

public class VectorStoreTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly VectorStore _store;

    public VectorStoreTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new ApplicationDbContext(options);
        _store = new VectorStore(_db, new FakeEmbeddingService());
    }

    public void Dispose() => _db.Dispose();

    private Task StoreAsync(string document, params (int page, string text)[] chunks) =>
        _store.ProcessAndStoreDocumentAsync(document, chunks
            .Select((c, i) => (c.page, i, c.text))
            .ToList());

    [Fact]
    public async Task ProcessAndStore_ReuploadingSameDocument_ReplacesOnlyItsChunks()
    {
        await StoreAsync("a.txt", (1, "alpha policy one"), (1, "alpha policy two"), (2, "alpha policy three"));
        await StoreAsync("b.txt", (1, "beta policy one"));
        await StoreAsync("a.txt", (1, "new alpha policy"), (2, "new alpha policy page two"));

        var aChunks = _db.DocumentChunks.Where(c => c.DocumentName == "a.txt").ToList();
        var bChunks = _db.DocumentChunks.Where(c => c.DocumentName == "b.txt").ToList();

        Assert.Equal(2, aChunks.Count);
        Assert.All(aChunks, c => Assert.StartsWith("new alpha", c.TextContent));
        Assert.Single(bChunks);
    }

    [Fact]
    public async Task Search_VectorOnly_IncludesChunksThatHybridFiltersOut()
    {
        await StoreAsync("guide.txt",
            (1, "students may drop two courses after week two with advisor approval"),
            (2, "drop drop drop unrelatedxyzzy foo bar"));

        var query = "how many courses can i drop after week two";

        var vectorOnly = await _store.SearchAsync(query, topK: 5, minScore: 0.25, mode: VectorSearchMode.VectorOnly);
        var hybrid = await _store.SearchAsync(query, topK: 5, minScore: 0.25, mode: VectorSearchMode.Hybrid);

        // Vector-only ranks the strong match first but also returns the weaker, keyword-poor chunk.
        Assert.Equal(1, vectorOnly[0].PageNumber);
        Assert.Contains(vectorOnly, r => r.PageNumber == 2);

        // Hybrid keeps the strong match (coverage 4/4) and drops the chunk whose keyword
        // coverage is 1/4 with cosine below the escape threshold.
        Assert.Single(hybrid);
        Assert.Equal(1, hybrid[0].PageNumber);
    }

    [Fact]
    public async Task Search_Hybrid_KeepsHighVectorScoreChunkEvenWithLowKeywordCoverage()
    {
        // Query tokens: course, drop, week, two. This chunk covers only "drop" (1/4 < 1/3)
        // but is made almost entirely of that token, so cosine >= 0.50 escape hatch applies.
        await StoreAsync("guide.txt",
            (3, "drop drop drop drop"));

        var results = await _store.SearchAsync(
            "how many courses can i drop after week two", topK: 5, minScore: 0.25, mode: VectorSearchMode.Hybrid);

        Assert.Contains(results, r => r.PageNumber == 3);
    }

    [Fact]
    public async Task Search_Hybrid_AppliesDiversityCapPerDocumentPage()
    {
        await StoreAsync("guide.txt",
            (1, "drop courses week two policy"),
            (1, "drop courses week two policy again"),
            (1, "drop courses week two policy once more"),
            (1, "drop courses week two policy finally"),
            (2, "drop courses week two policy elsewhere"));

        var results = await _store.SearchAsync(
            "how many courses can i drop after week two", topK: 5, minScore: 0.25, mode: VectorSearchMode.Hybrid);

        Assert.True(results.Count >= 3);
        var perPage = results.GroupBy(r => r.PageNumber);
        Assert.All(perPage, g => Assert.True(g.Count() <= 2, $"Page {g.Key} has {g.Count()} chunks, expected at most 2"));
    }

    [Fact]
    public async Task Search_MinScoreCutoff_ExcludesUnrelatedChunks()
    {
        await StoreAsync("guide.txt",
            (1, "students may drop two courses after week two"),
            (4, "giraffes elephants zebras kangaroos"));

        var results = await _store.SearchAsync(
            "how many courses can i drop after week two", topK: 5, minScore: 0.25, mode: VectorSearchMode.Hybrid);

        Assert.Single(results);
        Assert.Equal(1, results[0].PageNumber);
    }

    [Fact]
    public async Task Search_StopWordsInQuery_DoNotProduceMatchesOrCoverage()
    {
        await StoreAsync("guide.txt",
            (1, "week two refund policy"),      // covers week + two (2/3 of real keywords)
            (2, "refund policy policy policy"), // covers none of the real keywords
            (3, "after after after after"));    // only stop-words; must never match

        var results = await _store.SearchAsync(
            "courses after week two", topK: 5, minScore: 0.25, mode: VectorSearchMode.Hybrid);

        var page = Assert.Single(results);
        Assert.Equal(1, page.PageNumber);
    }

    [Fact]
    public async Task Search_AllStopWordQuery_ReturnsNoResultsAgainstStopWordOnlyChunks()
    {
        await StoreAsync("guide.txt", (1, "after after after"));

        var results = await _store.SearchAsync(
            "how many can i after the", topK: 5, minScore: 0.25, mode: VectorSearchMode.Hybrid);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_EmptyIndexOrInvalidArguments_ReturnsEmpty()
    {
        Assert.Empty(await _store.SearchAsync("anything", topK: 5, minScore: 0.25));

        await StoreAsync("guide.txt", (1, "some policy text"));

        Assert.Empty(await _store.SearchAsync("   ", topK: 5, minScore: 0.25));
        Assert.Empty(await _store.SearchAsync("policy", topK: 0, minScore: 0.25));
    }

    [Fact]
    public async Task Search_EmbeddingDimensionMismatch_SkipsIncompatibleChunksWithoutFailing()
    {
        _db.DocumentChunks.Add(new DocumentChunk
        {
            DocumentName = "legacy.pdf",
            PageNumber = 1,
            ChunkIndex = 0,
            TextContent = "legacy chunk with a different embedding dimension",
            EmbeddingJson = "[1.0, 2.0, 3.0]"
        });
        _db.DocumentChunks.Add(new DocumentChunk
        {
            DocumentName = "guide.txt",
            PageNumber = 1,
            ChunkIndex = 0,
            TextContent = "valid policy chunk about dropping courses",
            EmbeddingJson = System.Text.Json.JsonSerializer.Serialize(
                FakeEmbeddingService.Embed("valid policy chunk about dropping courses"))
        });
        await _db.SaveChangesAsync();

        var results = await _store.SearchAsync(
            "dropping courses policy", topK: 5, minScore: 0.25, mode: VectorSearchMode.Hybrid);

        var result = Assert.Single(results);
        Assert.Equal("guide.txt", result.DocumentName);
    }

    [Fact]
    public async Task DeleteDocument_RemovesOnlyThatDocument()
    {
        await StoreAsync("a.txt", (1, "alpha policy"));
        await StoreAsync("b.txt", (1, "beta policy"));

        await _store.DeleteDocumentAsync("a.txt");

        Assert.Empty(_db.DocumentChunks.Where(c => c.DocumentName == "a.txt"));
        Assert.Single(_db.DocumentChunks.Where(c => c.DocumentName == "b.txt"));
    }
}
