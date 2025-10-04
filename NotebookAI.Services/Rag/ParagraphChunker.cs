using Adventures.Shared.Documents;
using Adventures.Shared.Rag;
using NotebookAI.Services.Documents; // Added for BookDocument
using System.Text.RegularExpressions;

namespace NotebookAI.Services.Rag;

public sealed record BookChunk(
    string Id,
    string ParentId,
    int Sequence,
    string Text,
    string? Chapter,
    string? ParagraphRange,
    string? OwnerUserId,
    IReadOnlyDictionary<string,string>? Tags
) : IChunk;

public sealed class ParagraphChunker : IChunker<BookDocument, BookChunk>
{
    private static readonly Regex ChapterRegex = new("^Chapter\\s+([0-9IVXLC]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IEnumerable<BookChunk> Chunk(BookDocument doc, ChunkingOptions options)
    {
        // Simple paragraph split on blank lines
        var paragraphs = Regex.Split(doc.Content, "(\r?\n){2,}")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        string? currentChapter = null;
        int seq = 0;
        for (int i = 0; i < paragraphs.Count; i++)
        {
            var para = paragraphs[i];
            var chapMatch = ChapterRegex.Match(para);
            if (chapMatch.Success)
            {
                currentChapter = chapMatch.Groups[1].Value;
            }

            // Basic token-ish length cutoff
            if (para.Length > options.MaxTokens * 4) // rough heuristic
            {
                foreach (var sliced in Slice(para, options.MaxTokens * 4))
                {
                    yield return new BookChunk(
                        Id: $"{doc.Id}::c{seq}",
                        ParentId: doc.Id,
                        Sequence: seq++,
                        Text: sliced,
                        Chapter: currentChapter,
                        ParagraphRange: i.ToString(),
                        OwnerUserId: doc.Tags != null && doc.Tags.TryGetValue("owner", out var own) ? own : null,
                        Tags: doc.Tags
                    );
                }
            }
            else
            {
                yield return new BookChunk(
                    Id: $"{doc.Id}::p{seq}",
                    ParentId: doc.Id,
                    Sequence: seq++,
                    Text: para,
                    Chapter: currentChapter,
                    ParagraphRange: i.ToString(),
                    OwnerUserId: doc.Tags != null && doc.Tags.TryGetValue("owner", out var own) ? own : null,
                    Tags: doc.Tags
                );
            }
        }
    }

    private static IEnumerable<string> Slice(string text, int maxLen)
    {
        for (int i = 0; i < text.Length; i += maxLen)
        {
            yield return text.Substring(i, Math.Min(maxLen, text.Length - i));
        }
    }
}
