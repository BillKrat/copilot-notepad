using System.Collections.Concurrent;
using System.Text;
using Adventures.Shared.Documents;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Adventures.Shared.Rag;

public sealed class InMemoryRagService<TDocument> : IRagService<TDocument> where TDocument : IDocument
{
    private readonly Kernel _kernel;
    private readonly IDocumentStore<TDocument> _store;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embedder;

    private sealed record EmbeddedDoc(string Id, float[] Vector, TDocument Document);
    private readonly ConcurrentDictionary<string, EmbeddedDoc> _embedded = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryRagService(Kernel kernel, IDocumentStore<TDocument> store)
    {
        _kernel = kernel;
        _store = store;
        _embedder = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }

    public async Task<int> EnsureIndexedAsync(CancellationToken ct = default)
    {
        var all = await _store.GetAllAsync(ct);
        int added = 0;
        foreach (var d in all)
        {
            if (_embedded.ContainsKey(d.Id)) continue;
            var emb = (await _embedder.GenerateAsync([d.Content], cancellationToken: ct))[0];
            var vectorArray = emb.Vector.ToArray();
            if (_embedded.TryAdd(d.Id, new EmbeddedDoc(d.Id, vectorArray, d))) added++;
        }
        return added;
    }

    public async Task<string> AskAsync(string question, int top = 4, CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct);
        if (_embedded.IsEmpty)
        {
            return "No documents indexed yet.";
        }
        var queryEmbedding = (await _embedder.GenerateAsync([question], cancellationToken: ct))[0];
        var queryVec = queryEmbedding.Vector.ToArray();

        var ranked = _embedded.Values
            .Select(d => new { d.Document, Score = CosineSimilarity(queryVec, d.Vector) })
            .OrderByDescending(x => x.Score)
            .Take(Math.Max(1, top))
            .ToList();

        var sb = new StringBuilder();
        foreach (var r in ranked)
        {
            sb.AppendLine($"[Score {r.Score:F3}] {r.Document.Title} by {r.Document.Author} ({r.Document.Date:yyyy-MM-dd})");
            var snippet = r.Document.Content.Length > 600 ? r.Document.Content[..600] + "..." : r.Document.Content;
            sb.AppendLine(snippet);
            sb.AppendLine();
        }

        var context = sb.ToString();
        var prompt = $"You are a helpful assistant. Base answers only on the provided context unless generally accepted public knowledge is required.\nContext:\n{context}\nQuestion: {question}\nAnswer:";

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage("Answer using only the supplied context unless a universally known fact is needed.");
        history.AddUserMessage(prompt);
        var response = await chat.GetChatMessageContentAsync(history, kernel: _kernel, cancellationToken: ct);
        return response.Content ?? string.Empty;
    }

    private static double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count != b.Count) return 0;
        double dot = 0; double na = 0; double nb = 0;
        for (int i = 0; i < a.Count; i++)
        {
            var av = a[i]; var bv = b[i];
            dot += av * bv; na += av * av; nb += bv * bv;
        }
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-9);
    }
}
