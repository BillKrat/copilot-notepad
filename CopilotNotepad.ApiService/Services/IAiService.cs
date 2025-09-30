namespace CopilotNotepad.ApiService.Services;

/// <summary>
/// Interface for AI service integration (future OpenAI integration)
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Enhance note content using AI
    /// </summary>
    /// <param name="content">Original note content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Enhanced content</returns>
    Task<string> EnhanceContentAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate summary for a note
    /// </summary>
    /// <param name="content">Note content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summary</returns>
    Task<string> GenerateSummaryAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate title suggestions based on content
    /// </summary>
    /// <param name="content">Note content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of title suggestions</returns>
    Task<IEnumerable<string>> GenerateTitleSuggestionsAsync(string content, CancellationToken cancellationToken = default);
}

/// <summary>
/// Mock implementation of AI service for development
/// </summary>
public class MockAiService : IAiService
{
    public Task<string> EnhanceContentAsync(string content, CancellationToken cancellationToken = default)
    {
        // Mock implementation - in the future this will call OpenAI
        return Task.FromResult($"Enhanced: {content}");
    }

    public Task<string> GenerateSummaryAsync(string content, CancellationToken cancellationToken = default)
    {
        // Mock implementation
        var words = content.Split(' ').Take(10);
        return Task.FromResult($"Summary: {string.Join(' ', words)}...");
    }

    public Task<IEnumerable<string>> GenerateTitleSuggestionsAsync(string content, CancellationToken cancellationToken = default)
    {
        // Mock implementation
        var suggestions = new[]
        {
            "Note Title",
            "My Thoughts",
            "Important Notes"
        };
        return Task.FromResult<IEnumerable<string>>(suggestions);
    }
}