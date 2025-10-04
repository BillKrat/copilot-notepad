using Microsoft.Extensions.AI; // New abstractions
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NotebookAI.Services.Interfaces;
using System.Collections.Concurrent;
using System.Text;

namespace NotebookAI.Services.Models;

public sealed class SaintsRagService
{
    private readonly Kernel _kernel;
    private readonly ISaintsDocumentStore _store;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embedder; // Updated type

    private readonly ConcurrentDictionary<string, EmbeddedDoc> _embedded = new(StringComparer.OrdinalIgnoreCase);
    private sealed record EmbeddedDoc(string Id, string Title, string Author, DateTime Date, float[] Vector, string Content);

    public SaintsRagService(Kernel kernel, ISaintsDocumentStore store)
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
            var generated = await _embedder.GenerateAsync([d.Content], cancellationToken: ct);
            var emb = generated[0];
            var vectorArray = emb.Vector.ToArray();
            var doc = new EmbeddedDoc(d.Id, d.Title, d.Author, d.Date, vectorArray, d.Content);
            if (_embedded.TryAdd(d.Id, doc)) added++;
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

        var generated = await _embedder.GenerateAsync([question], cancellationToken: ct);
        var queryEmbedding = generated[0];
        var queryVec = queryEmbedding.Vector.ToArray();

        var ranked = _embedded.Values
            .Select(d => new { Doc = d, Score = CosineSimilarity(queryVec, d.Vector) })
            .OrderByDescending(x => x.Score)
            .Take(Math.Max(1, top))
            .ToList();

        var sb = new StringBuilder();
        foreach (var r in ranked)
        {
            sb.AppendLine($"[Score {r.Score:F3}] {r.Doc.Title} by {r.Doc.Author} ({r.Doc.Date:yyyy-MM-dd})");
            var snippet = r.Doc.Content.Length > 600 ? r.Doc.Content[..600] + "..." : r.Doc.Content;
            sb.AppendLine(snippet);
            sb.AppendLine();
        }

        var context = sb.ToString();
        var prompt = $"You are a helpful assistant answering questions based only on the provided primary source excerpts from saints. If the context is insufficient, say you don't have enough information.\nContext:\n{context}\nQuestion: {question}\nAnswer:";

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage("Answer using only the supplied context unless a universally known fact of Christian theology is needed to clarify.");
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
