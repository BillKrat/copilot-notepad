# Vision Statement (NotebookAI)
NotebookAI enables users to:
- Upload and manage multiple text sources (e.g., books such as a Saint's writings).
- Take structured study notes (blog-style entries) referencing book chapter + paragraph.
- Query across their own books + notes.
- (Optionally) subscribe to and query other users' public/shared notes.
- Receive grounded AI answers with citations back to book positions and note sources.

Key goals:
- Extensible: add new document types without modifying the core.
- Replaceable storage/index layers.
- Clear interfaces for ingestion, enrichment, retrieval, answer synthesis.

---
# AI for Dummies (Project Primer)

## 1. Current RAG Wiring (Program.cs)
Current DI registrations composing the evolving RAG pipeline:
```csharp
// Document & RAG wiring
builder.Services.AddSingleton<IBookDocumentStore, InMemoryBookDocumentStore>();
builder.Services.AddSingleton(typeof(IDocumentStore<>), typeof(InMemoryDocumentStore<>) );
builder.Services.AddSingleton<IChunker<BookDocument, BookChunk>, ParagraphChunker>();
builder.Services.AddSingleton<IVectorIndex, InMemoryVectorIndex>();
builder.Services.AddSingleton<IAdvancedRagService, HybridRagService>();
```
Summary:
- Book storage: in-memory (non-persistent) via generic store pattern.
- Chunking: `ParagraphChunker` splits content into paragraph-sized `BookChunk`s.
- Vector index: placeholder `InMemoryVectorIndex` (brute-force cosine) – not yet used fully for search (HybridRagService re-embeds per query; to optimize later).
- Advanced RAG orchestration: `HybridRagService` (multi-stage flow – currently simplified and synchronous).

Next optimization milestone: persist and reuse chunk embeddings (store vector once, query index directly).

## 2. Original Minimal Wiring (Historical Reference)
(Previously used only whole-document embeddings.)
```csharp
builder.Services.AddSingleton<IRagService<BookDocument>, InMemoryRagService<BookDocument>>();
```
Replaced now by chunked + advanced structure.

## 3. Chunking (Recap)
Chunking = splitting large documents into smaller, semantically meaningful segments before embedding.
See section 6.3 for the chunk model implemented (`BookChunk`).

## 4. Vector Index Abstraction
Interface (`IVectorIndex`) added; current implementation: `InMemoryVectorIndex`.
Future: swap for pgvector / Azure AI Search / Qdrant without touching `HybridRagService` consumers.

## 5. Forms of Persistence
(Planned evolution – currently still in-memory.)
1. InMemory -> 2. Relational / Document DB -> 3. Vector DB / Hybrid search -> 4. Distributed cache + background pipelines.

## 6. Advanced RAG Strategy (Implemented Skeleton)
Key components now in place:
- `RagQuery`, `RagAnswer`, `Citation` models.
- `IAdvancedRagService` abstraction.
- `HybridRagService` prototype (naive vector scoring; re-embeds chunks each query – to be optimized).
- `IChunker` + `ParagraphChunker` splitting by blank lines, capturing chapter markers.
- `IVectorIndex` + `InMemoryVectorIndex` (embeddings not yet persisted per chunk in sample flow).

Flow (current prototype):
```
Query -> Ensure books chunked -> For each chunk: embed on demand -> Score vs question -> Take TopK -> Build cited prompt -> LLM -> Answer
```
Planned improvement:
```
Upload/Update -> Chunk -> Embed once -> Upsert vector index -> Query -> VectorIndex.SimilaritySearch -> (optional) Rerank -> Prompt -> Answer
```

## 7. Gap Analysis (What to Improve Next)
| Area | Current | Target |
|------|---------|--------|
| Embedding reuse | Re-embeds every query | Precompute + store vectors in `IVectorIndex` |
| Vector search | Manual loop cosine | ANN / Hybrid search via index |
| Multi-corpus | Books only | Books + Notes + Subscriptions |
| Access control | Not enforced | Metadata filter (owner, visibility) |
| Citations | Basic bracket labels | Source typed (Book/Note) + stable ids |
| Caching | None | Chunk + embedding cache (memory + distributed) |
| Reranking | None | Optional cross-encoder stage |
| Observability | Basic diagnostics counts | Timing, stage metrics, cache hit ratio |
| Background indexing | Inline in query path | Hosted service pipeline |

## 8. Immediate Next Steps (Recommended Order)
1. Persist chunk embeddings in `IVectorIndex` during EnsureIndexedAsync (remove per-query re-embedding).
2. Add note documents (`NoteDocument`, `INoteDocumentStore`).
3. Introduce visibility + ownership metadata (UserId, Visibility enum) in documents & chunks.
4. Implement metadata filters in retrieval (limit search space before scoring).
5. Replace `InMemoryVectorIndex` with pgvector or Azure AI Search (hybrid lexical + vector).
6. Add reranking layer (optional) before final prompt assembly.
7. Add citations enrichment: return stable chunk ids and highlight ranges.
8. Introduce background indexing hosted service + queue (Channel<T> or durable store).
9. Add streaming token support for answers.
10. Add structured evaluation diagnostics (prompt tokens, retrieval latency).

## 9. Longer-Term Enhancements
- Pluggable `IChunker` strategies (semantic, sentence, sliding window).
- Hybrid retrieval (BM25 + vector) scoring fusion.
- Personalization (boost user’s frequently cited sources).
- Feedback loop (thumbs up/down -> reranker fine-tuning data).
- Summarization / outline generation for large newly ingested books.
- Embedding versioning & re-embedding scheduler.

## 10. Security & Multi-Tenancy Roadmap
Add fields:
```csharp
public enum Visibility { Private, Shared, Public }
```
Document / Chunk metadata additions:
- OwnerUserId (string)
- Visibility (enum)
- SharedWith (optional list) – or many-to-many link table later.
Apply at retrieval (filter out unauthorized before scoring) + at index time (index only permitted embeddings or store ACL metadata for filtering).

## 11. Migration Path Snapshot
```
Current (Prototype)
  InMemoryBookDocumentStore + ParagraphChunker + HybridRagService (re-embedding)
Next
  + Persist embeddings in VectorIndex
  + NoteDocument support
  + Ownership / visibility filtering
  + Real vector backend
Future
  + Hybrid + Rerank + Background indexing + Streaming
  + Multi-tenant security + Observability dashboards
```

## 12. Summary
The project now has a scaffolded advanced RAG architecture: models, chunking, vector index abstraction, advanced orchestrator. Next engineering work should focus on eliminating per-query embedding cost, adding multi-corpus + security metadata, and adopting a persistent vector search backend.

(End)
