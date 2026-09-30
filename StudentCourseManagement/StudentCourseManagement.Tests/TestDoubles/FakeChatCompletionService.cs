using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace StudentCourseManagement.Tests.TestDoubles;

/// <summary>
/// Scripted offline chat model for integration tests. It follows the RAG policies of the real
/// prompt: when the retrieved documents block is empty it replies with the exact refusal phrase,
/// otherwise it answers by citing the first [Source: ...] line found in the prompt. Tests can
/// also inspect <see cref="ReceivedPrompts"/> to assert on how the prompt was built.
/// </summary>
public class FakeChatCompletionService : IChatCompletionService
{
    // Matches real retrieved sources like "[Source: handbook.txt, Page: 2]" but skips the
    // brace-bearing instruction template "[Source: {DocumentName}, Page: {PageNumber}]".
    private static readonly Regex SourceRegex = new(@"\[Source: ([^\]{}]+)\]", RegexOptions.Compiled);

    public List<string> ReceivedPrompts { get; } = new();

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = string.Concat(chatHistory.Select(m => m.Content ?? string.Empty));
        ReceivedPrompts.Add(prompt);

        return Task.FromResult<IReadOnlyList<ChatMessageContent>>(
            new List<ChatMessageContent> { BuildReply(prompt) });
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = string.Concat(chatHistory.Select(m => m.Content ?? string.Empty));
        ReceivedPrompts.Add(prompt);

        yield return new StreamingChatMessageContent(AuthorRole.Assistant, BuildReply(prompt).Content);
    }

    private static ChatMessageContent BuildReply(string prompt)
    {
        string reply;

        if (prompt.Contains("No relevant handbook passages were found", StringComparison.Ordinal))
        {
            reply = "{\"matchedCourses\":[],\"advisorNote\":\"not found in the documents\"}";
        }
        else
        {
            // Only cite sources from the retrieved documents block, never the citation-format
            // example used in the prompt instructions.
            var start = prompt.IndexOf("<documents>", StringComparison.Ordinal);
            var end = prompt.IndexOf("</documents>", StringComparison.Ordinal);
            var documents = start >= 0 && end > start
                ? prompt.Substring(start, end - start)
                : prompt;

            var match = SourceRegex.Match(documents);
            reply = match.Success
                ? $"{{\"matchedCourses\":[],\"advisorNote\":\"According to the handbook [Source: {match.Groups[1].Value}].\"}}"
                : "{\"matchedCourses\":[],\"advisorNote\":\"no sources provided\"}";
        }

        return new ChatMessageContent(AuthorRole.Assistant, reply);
    }
}
