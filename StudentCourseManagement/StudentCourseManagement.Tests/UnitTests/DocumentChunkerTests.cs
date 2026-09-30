using StudentCourseManagement.Application.Services;
using Xunit;

namespace StudentCourseManagement.Tests.UnitTests;

public class DocumentChunkerTests
{
    private readonly DocumentChunker _chunker = new();

    [Fact]
    public void ChunkText_TextShorterThanChunkSize_ReturnsSingleUnchangedChunk()
    {
        var text = "Students may drop up to two courses after week two.";

        var chunks = _chunker.ChunkText(text, 2000, 200);

        Assert.Single(chunks);
        Assert.Equal(text, chunks[0]);
    }

    [Fact]
    public void ChunkText_EmptyOrNullText_ReturnsNoChunks()
    {
        Assert.Empty(_chunker.ChunkText(string.Empty, 2000, 200));
        Assert.Empty(_chunker.ChunkText("   \r\n  ", 2000, 200));
    }

    [Fact]
    public void ChunkText_LongText_EveryChunkRespectsChunkSizeLimit()
    {
        var text = string.Join(" ", Enumerable.Repeat("lorem ipsum dolor sit amet consectetur adipiscing elit", 200));

        var chunks = _chunker.ChunkText(text, 500, 100);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= 500, $"Chunk of length {c.Length} exceeds limit"));
    }

    private static string UniqueWordText(int wordCount) =>
        string.Join(" ", Enumerable.Range(0, wordCount).Select(i => $"word{i:d5}"));

    [Fact]
    public void ChunkText_LongText_ConsecutiveChunksOverlap()
    {
        var text = UniqueWordText(600);

        var chunks = _chunker.ChunkText(text, 400, 80);

        Assert.True(chunks.Count > 1);
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            int currentIndex = text.IndexOf(chunks[i], StringComparison.Ordinal);
            int nextIndex = text.IndexOf(chunks[i + 1], StringComparison.Ordinal);

            Assert.True(currentIndex >= 0, $"Chunk {i} not found in original text");
            Assert.True(nextIndex > currentIndex, "Chunks must advance through the text");

            int currentEnd = currentIndex + chunks[i].Length;
            Assert.True(nextIndex < currentEnd,
                $"Chunks {i} and {i + 1} do not overlap: next starts at {nextIndex}, current ends at {currentEnd}");
        }
    }

    [Fact]
    public void ChunkText_LongText_CoversEntireTextFromStartToEnd()
    {
        var text = UniqueWordText(600);

        var chunks = _chunker.ChunkText(text, 400, 80);

        int firstIndex = text.IndexOf(chunks[0], StringComparison.Ordinal);
        var last = chunks[^1];
        int lastIndex = text.IndexOf(last, StringComparison.Ordinal);

        Assert.Equal(0, firstIndex);
        Assert.Equal(text.Length, lastIndex + last.Length);
    }

    [Fact]
    public void SplitTxtIntoPages_TextWithFormFeeds_ReturnsOneBasedPageNumberedText()
    {
        var text = "Drop policy page.\fRefund policy page.\fLibrary hours page.";

        var pages = _chunker.SplitTxtIntoPages(text);

        Assert.Equal(3, pages.Count);
        Assert.Equal((1, "Drop policy page."), pages[0]);
        Assert.Equal((2, "Refund policy page."), pages[1]);
        Assert.Equal((3, "Library hours page."), pages[2]);
    }

    [Fact]
    public void SplitTxtIntoPages_TextWithoutFormFeeds_ReturnsSinglePage()
    {
        var text = "Everything is one long page of text.";

        var pages = _chunker.SplitTxtIntoPages(text);

        var page = Assert.Single(pages);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(text, page.Text);
    }

    [Fact]
    public void SplitTxtIntoPages_TrailingOrRepeatedFormFeeds_DoNotCreateEmptyPages()
    {
        var text = "Page one.\f\fPage two.\f";

        var pages = _chunker.SplitTxtIntoPages(text);

        Assert.Equal(2, pages.Count);
        Assert.Equal("Page one.", pages[0].Text);
        Assert.Equal("Page two.", pages[1].Text);
    }

    [Fact]
    public void SplitTxtIntoPages_EmptyText_ReturnsNoPages()
    {
        Assert.Empty(_chunker.SplitTxtIntoPages(string.Empty));
        Assert.Empty(_chunker.SplitTxtIntoPages("  \f \f "));
    }
}
