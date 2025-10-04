namespace Adventures.Shared.Rag;

public sealed record RagQuery(
    string Question,
    string UserId,
    IReadOnlyCollection<string>? BookIds = null,
    bool IncludeOwnNotes = true,
    bool IncludeSubscribedNotes = true,
    int TopK = 8,
    bool IncludeCitations = true);

public sealed record Citation(
    string SourceId,
    string SourceType,
    string? Chapter,
    string? ParagraphRange,
    double Score,
    string Snippet);

public sealed record RagAnswer(
    string Answer,
    IReadOnlyList<Citation> Citations,
    IReadOnlyDictionary<string,object>? Diagnostics);

public interface IAdvancedRagService
{
    Task<RagAnswer> QueryAsync(RagQuery query, CancellationToken ct = default);
}
