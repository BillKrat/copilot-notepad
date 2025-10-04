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
        var qEmb = (await _embedder.GenerateAsync([query.Question], cancellationToken: ct))[0].Vector.ToArray();

        // 3. Vector search across all chunk ids we stored
        var allChunkIds = _chunkCache.Values.SelectMany(v => v.Chunks.Select(c => c.Id)).ToList();
        var matches = new List<VectorMatch>();
        foreach (var id in allChunkIds)
        {
            // This naive implementation iterates store; for real index use, index would handle search directly.
            // Here we fetch stored vectors indirectly by recomputing when indexing occurred (simplified: skip)
        }
        // Since we don't keep vectors here (index does), do a single index search call per naive approach is not possible without storing vectors.
        // Simplify: re-embed each chunk text (inefficient but illustrative) then score in-process.
        var chunkList = _chunkCache.Values.SelectMany(v => v.Chunks).ToList();
        var results = new List<(BookChunk Chunk, double Score)>();
        foreach (var ch in chunkList)
        {
            var chEmb = (await _embedder.GenerateAsync([ch.Text], cancellationToken: ct))[0].Vector.ToArray();
            var score = Cos(qEmb, chEmb);
            results.Add((ch, score));
        }
        var top = results.OrderByDescending(r => r.Score).Take(Math.Max(1, query.TopK)).ToList();

        // 4. Assemble context & citations
        var sb = new StringBuilder();
        var citations = new List<Citation>();
        int citeIndex = 1;
        foreach (var t in top)
        {
            var label = $"B{citeIndex++}";
            sb.AppendLine($"[{label}] (Book {t.Chunk.ParentId} Ch {t.Chunk.Chapter ?? "?"} Para {t.Chunk.ParagraphRange})");
            sb.AppendLine(t.Chunk.Text.Length > 800 ? t.Chunk.Text[..800] + "..." : t.Chunk.Text);
            sb.AppendLine();
            citations.Add(new Citation(
                SourceId: t.Chunk.ParentId,
                SourceType: "Book",
                Chapter: t.Chunk.Chapter,
                ParagraphRange: t.Chunk.ParagraphRange,
                Score: t.Score,
                Snippet: t.Chunk.Text.Length > 160 ? t.Chunk.Text[..160] + "..." : t.Chunk.Text
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
            ["chunksExamined"] = results.Count,
            ["topCount"] = top.Count
        });
    }

    private Task EnsureIndexedAsync(IEnumerable<BookDocument> books, CancellationToken ct)
    {
        foreach (var b in books)
        {
            var hash = b.Content.Length; // naive hash placeholder
            if (_chunkCache.TryGetValue(b.Id, out var existing) && existing.Hash == hash)
            {
                continue; // already indexed
            }
            var chunks = _chunker.Chunk(b, new ChunkingOptions()).ToList();
            _chunkCache[b.Id] = (hash, chunks);
            // In a real implementation, generate & upsert chunk embeddings to _index here.
        }
        return Task.CompletedTask;
    }

    private static double Cos(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count != b.Count) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Count; i++) { var av = a[i]; var bv = b[i]; dot += av * bv; na += av * av; nb += bv * bv; }
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-9);
    }
}
