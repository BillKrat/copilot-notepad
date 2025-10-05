using Adventures.Shared.Documents;
using Adventures.Shared.Rag;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NotebookAI.Services.Documents; // Added for BookDocument
using System.Collections.Concurrent;
using System.Text;

namespace NotebookAI.Services.Rag;

public sealed class HybridRagService : IAdvancedRagService
{
    private readonly Kernel _kernel;
    private readonly IDocumentStore<BookDocument> _books;
    private readonly IChunker<BookDocument, BookChunk> _chunker;
    private readonly IVectorIndex _index;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embedder;

    private readonly ConcurrentDictionary<string, (int Hash, List<BookChunk> Chunks)> _chunkCache = new(StringComparer.OrdinalIgnoreCase);

    public HybridRagService(
        Kernel kernel,
        IDocumentStore<BookDocument> books,
        IChunker<BookDocument, BookChunk> chunker,
        IVectorIndex index)
    {
        _kernel = kernel;
        _books = books;
        _chunker = chunker;
        _index = index;
        _embedder = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }

    public async Task<RagAnswer> QueryAsync(RagQuery query, CancellationToken ct = default)
    {
        // 1. Ensure indices for relevant books
        var corpus = await _books.GetAllAsync(ct);
        var filtered = corpus.Where(b => query.BookIds == null || query.BookIds.Contains(b.Id)).ToList();
        await EnsureIndexedAsync(filtered, ct);

        // 2. Embed question
        var qEmb = (await _embedder.GenerateAsync([query.Question], cancellationToken: ct))[0].Vector;

        // 3. Query vector index (topK + maybe some extra for filtering)
        var matches = await _index.SimilaritySearchAsync(qEmb, Math.Max(1, query.TopK), ct: ct);

        // 4. Map back to chunks
        var chunkDict = _chunkCache.Values.SelectMany(v => v.Chunks).ToDictionary(c => c.Id, c => c);
        var topChunks = matches
            .Select(m =>
            {
                if (chunkDict.TryGetValue(m.Id, out var ch))
                {
                    BookChunk? found = ch;
                    return (found, (double)m.Score);
                }
                return ( (BookChunk?)null, 0d );
            })
            .Where(t => t.Item1 != null)
            .Select(t => (Chunk: t.Item1!, Score: t.Item2))
            .ToList();

        // 5. Assemble context & citations
        var sb = new StringBuilder();
        var citations = new List<Citation>();
        int citeIndex = 1;
        foreach (var t in topChunks)
        {
            var chunk = t.Chunk!; // filtered above
            var label = $"B{citeIndex++}";
            sb.AppendLine($"[{label}] (Book {chunk.ParentId} Ch {chunk.Chapter ?? "?"} Para {chunk.ParagraphRange})");
            sb.AppendLine(chunk.Text.Length > 800 ? chunk.Text[..800] + "..." : chunk.Text);
            sb.AppendLine();
            citations.Add(new Citation(
                SourceId: chunk.ParentId,
                SourceType: "Book",
                Chapter: chunk.Chapter,
                ParagraphRange: chunk.ParagraphRange,
                Score: t.Score,
                Snippet: chunk.Text.Length > 160 ? chunk.Text[..160] + "..." : chunk.Text
            ));
        }

        var prompt = $"Use ONLY the provided context. Cite sources inline using their bracket labels (e.g., [B1]). If insufficient information, say so.\nContext:\n{sb}\nQuestion: {query.Question}\nAnswer:";

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage("Answer based strictly on context; include bracket citations.");
        history.AddUserMessage(prompt);
        var response = await chat.GetChatMessageContentAsync(history, kernel: _kernel, cancellationToken: ct);

        return new RagAnswer(response.Content ?? string.Empty, citations, new Dictionary<string, object>
        {
            ["chunksExamined"] = _chunkCache.Sum(c => c.Value.Chunks.Count),
            ["topCount"] = topChunks.Count
        });
    }

    private async Task EnsureIndexedAsync(IEnumerable<BookDocument> books, CancellationToken ct)
    {
        foreach (var b in books)
        {
            if (ct.IsCancellationRequested) break;
            var hash = b.Content.Length; // naive hash placeholder
            if (_chunkCache.TryGetValue(b.Id, out var existing) && existing.Hash == hash)
            {
                continue; // already indexed
            }
            var chunks = _chunker.Chunk(b, new ChunkingOptions()).ToList();
            _chunkCache[b.Id] = (hash, chunks);

            // Generate embeddings & upsert into vector index
            foreach (var batch in Batch(chunks, 8))
            {
                var texts = batch.Select(c => c.Text).ToArray();
                var embeddings = await _embedder.GenerateAsync(texts, cancellationToken: ct);
                for (int i = 0; i < batch.Count; i++)
                {
                    var ch = batch[i];
                    var emb = embeddings[i].Vector;
                    await _index.UpsertAsync(ch.Id, emb, new Dictionary<string, object>
                    {
                        ["bookId"] = ch.ParentId,
                        ["chapter"] = ch.Chapter ?? string.Empty,
                        ["para"] = ch.ParagraphRange ?? string.Empty
                    }, ct);
                }
            }
        }
    }

    private static IEnumerable<List<T>> Batch<T>(IEnumerable<T> source, int size)
    {
        var bucket = new List<T>(size);
        foreach (var item in source)
        {
            bucket.Add(item);
            if (bucket.Count == size)
            {
                yield return bucket;
                bucket = new List<T>(size);
            }
        }
        if (bucket.Count > 0) yield return bucket;
    }
}
