using Adventures.Shared.Documents;

namespace Adventures.Shared.Rag;

public sealed record ChunkingOptions(int MaxTokens = 512, int OverlapTokens = 32, bool PreserveParagraphs = true);

public interface IChunk
{
    string Id { get; }
    string ParentId { get; }
    int Sequence { get; }
    string Text { get; }
    string? Chapter { get; }
    string? ParagraphRange { get; }
    string? OwnerUserId { get; }
    IReadOnlyDictionary<string,string>? Tags { get; }
}

public interface IChunker<in TDocument, TChunk>
    where TDocument : IDocument
    where TChunk : IChunk
{
    IEnumerable<TChunk> Chunk(TDocument doc, ChunkingOptions options);
}
