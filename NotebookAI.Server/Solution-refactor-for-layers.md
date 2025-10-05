# Solution Refactor Plan (Controller > BLL > DAL + Clear Domain & Infrastructure Segregation)

## 1. Target Layering (Clean / Onion Hybrid)
- Presentation/API: `NotebookAI.Server` (controllers, filters, API models) – depends only on Application & Shared abstractions.
- Application (Business Logic Layer / BLL): new project `NotebookAI.Application` (or repurpose a lean subset of `NotebookAI.Services` after extraction). Contains:
  - Use cases / services (e.g., `BookCatalogService`, `RagQueryService`)
  - Orchestrates transactions, validation, policies
  - Interfaces it depends on: repositories, vector index, triple store abstractions
  - Maps between API DTOs and domain models
- Domain (Entities + Value Objects + Interfaces): keep or extend `Adventures.Shared` (rename later to `NotebookAI.Domain` if desired):
  - Pure models: `BookDocument`, `BookChunk`, `RagQuery`, `RagAnswer`, value objects
  - Abstractions: `IBookRepository`, `IVectorIndex`, `ITripleStoreRepository`, `IFileAssetStore`, `IChunker<T>`, `IRagService` (basic), `IAdvancedRagService`
  - Zero infrastructure, no EF, no HTTP, no Semantic Kernel specifics
- Infrastructure (DAL / Providers): consolidate or split
  - `NotebookAI.Data` (EF Core relational persistence: implements `IBookRepository`, migrations)
  - `NotebookAI.Triples` (triple store implementation: `ITripleStoreRepository`, seeding, ontology)
  - `NotebookAI.Files` (if needed; currently in `NotebookAI.Triples.Files` — extract to `NotebookAI.Storage` for file/blob abstraction implementing `IFileAssetStore`)
  - `NotebookAI.VectorIndex` (optional future: different implementations of `IVectorIndex` – in-memory, pgvector, Qdrant)
  - `NotebookAI.AI` (infrastructure adapters for LLM + embeddings; current `AddAiKernel` moved here; exposes abstraction `IAiKernelFactory` or registers `Kernel` as scoped)
  - These projects depend only on Domain abstractions & external packages
- Composition Root: `NotebookAI.Server` wires everything via DI.

Dependency Rule: Presentation -> Application -> Domain. Infrastructure depends on Domain only; Application depends on Domain & domain interfaces; Infrastructure never depends on Application or Presentation.

```
NotebookAI.Server
  -> NotebookAI.Application
      -> Adventures.Shared (Domain)
  -> NotebookAI.Data (Infra) ----?
  -> NotebookAI.Triples (Infra) -?--> all depend on Adventures.Shared
  -> NotebookAI.Storage (Infra)-?
  -> NotebookAI.AI (Infra)
```

## 2. Current Project Mapping & Actions
| Existing Project | Future Role | Action |
|------------------|-------------|-------|
| Adventures.Shared | Domain + some cross-cut | Trim to pure domain + interfaces; move any infra helpers out |
| NotebookAI.Services | Mixed (BLL + infra RAG) | Split: move orchestrations to Application; move adapters to infra subprojects |
| NotebookAI.Data | Infra (relational) | Keep; ensure only domain refs; remove direct controller usage |
| NotebookAI.Triples | Infra (semantic/triple store) | Keep; expose interface implementations only |
| NotebookAI.Server | Presentation | Remove direct use of persistence/AI types; only service DI + controllers |
| NotebookAI.AppHost / ServiceDefaults | Hosting / cross-cut | Keep; ensure no domain leakage |
| NotebookAI.Ftp | Infra (optional) | Treat as specialized file provider implementing `IFileAssetStore` |

## 3. Interface Inventory (Domain)
Create/centralize in Domain:
- Repositories: `IBookRepository`, (future) `IBookMetadataRepository`
- Knowledge: `ITripleStoreRepository`
- Indexing: `IVectorIndex`
- Storage: `IFileAssetStore`
- Chunking: `IChunker<TDoc,TChunk>`
- RAG: `IRagService<TDoc>`, `IAdvancedRagService`
- AI: `IEmbeddingService` (facade over `IEmbeddingGenerator`), `IChatCompletionService` abstraction wrapper if decoupling from Semantic Kernel

## 4. Application Services (Examples)
- `BookIngestionService` (validate, chunk, persist, index embeddings)
- `BookQueryService` / `RagQueryService` (coordinate vector + triple metadata + AI answer)
- `BookCatalogService` (CRUD/search books)
- `OntologySeedService` (invokes infra seeding behind interface; host as hosted service)

Each service depends only on domain interfaces—never concrete infra types.

## 5. Data Flow Example (RAG Query)
Controller (API DTO) -> `RagQueryService` (Application) -> repositories (`IBookRepository`) + `IVectorIndex` + `IEmbeddingService` -> domain objects aggregated -> answer -> mapped DTO -> Controller response.

## 6. Refactor Steps (Incremental)
1. Create new `NotebookAI.Application` project (.NET 9) referencing `Adventures.Shared` only.
2. Move pure orchestration classes from `NotebookAI.Services` (e.g., logic inside `HybridRagService` minus direct SK access) into Application; wrap SK calls behind interfaces in Domain + infra adapter.
3. Extract any EF/DbContext, triple, file, AI kernel concerns from `NotebookAI.Services` into respective infra projects.
4. Prune `Adventures.Shared` to remove AI kernel building & runtime service location; keep only models + contracts.
5. Introduce DTO layer in Server: Request/Response records (avoid leaking domain if you anticipate versioning).
6. Replace direct `Kernel` injection in Application with an abstraction (`IAiChatClient`, `IEmbeddingService`). Implement these in `NotebookAI.AI` using Semantic Kernel.
7. Register DI in `Program.cs` using extension methods per layer (`AddApplicationServices`, `AddDataInfrastructure`, etc.).
8. Add unit tests for Application services mocking domain interfaces (new test project `NotebookAI.Application.Tests`).
9. Rename or remove obsolete `NotebookAI.Services` after migration.
10. Introduce solution analyzers (optional) to enforce reference direction (e.g., NetArchTest, Roslyn analyzers) to prevent regressions.

## 7. DI Registration Pattern
```
services
  .AddDomain()              // if needed: validators, domain services
  .AddApplication()         // application service classes
  .AddDataInfrastructure()  // EF repos
  .AddTripleStore()         // triple store repo
  .AddVectorIndex()         // in-memory or external index
  .AddFileStorage()         // file/blob provider
  .AddAiInfrastructure();   // chat + embedding providers
```
All lifetimes: repositories usually scoped; stateless helpers singleton; heavy clients (HTTP) singleton; Application services scoped.

## 8. Transaction & Consistency
- Add `IUnitOfWork` in Domain if multi-repository commits are required; implemented by EF Data project.
- Application service boundary = transaction boundary.

## 9. Mapping Strategy
- Keep domain models expressive; add API DTOs (e.g., `BookDto`, `RagAnswerDto`).
- Use lightweight manual mapping for clarity; consider Mapster if duplication grows.

## 10. Error & Result Handling
- Domain exceptions: specific (e.g., `BookNotFoundException`).
- Application returns `Result<T>` / discriminated union (Success|ValidationError|NotFound|Conflict).
- Controllers translate to HTTP (problem details).

## 11. Testing Matrix
| Layer | Test Type |
|-------|-----------|
| Domain | Pure unit tests (no mocks needed) |
| Application | Unit tests with mocked interfaces |
| Infrastructure | Integration tests (db, triple store, vector index) |
| Presentation | Minimal controller tests / e2e (optional) |

## 12. RAG Separation
- Index building pipeline: `IEmbeddingPipeline` (Application) uses `IChunker`, `IEmbeddingService`, `IVectorIndex`, repositories.
- Query pipeline: `IRagQueryOrchestrator` -> retrieves candidate docs, vector search, chat generation.
- Keep hybrid vs basic variants behind strategy pattern if you need multiple algorithms.

## 13. Naming / Namespace Conventions
- Domain: `NotebookAI.Domain.*` (future rename) currently `Adventures.Shared.*`
- Application: `NotebookAI.Application.*`
- Infrastructure: `NotebookAI.Data.*`, `NotebookAI.Triples.*`, `NotebookAI.AI.*`, `NotebookAI.Storage.*`
- Presentation: `NotebookAI.Server.*`

## 14. Migration Considerations
- Preserve public API contracts by adding DTOs before removing domain type exposure.
- Mark old service registrations `[Obsolete]` for a deprecation window.
- Update solution folder structure for clarity (Solution Folders: `01-Domain`, `02-Application`, `03-Infrastructure`, `04-Presentation`, `05-Tests`).

## 15. Immediate Cleanups (Low Risk Wins)
- Change `HybridRagService` to depend on abstractions only; move SK direct calls to adapter.
- Remove controllers' direct knowledge of in-memory or EF specifics.
- Centralize vector index registration via `AddVectorIndex` extension.

## 16. Future Extensions
- Add caching layer (`IDocumentCache`) in Application (decorator) without changing controllers.
- Support multi-index (books, blog posts) by generic repository pattern + typed indices.
- Introduce background processing (queue) for embedding pipeline to decouple ingestion latency.

## 17. Definition of Done for Refactor Phase 1
- New Application project created & referenced by Server.
- Controllers depend only on Application interfaces.
- Domain project contains no EF/AI/triple specific code.
- Infrastructure projects compile using only Domain abstractions.
- All previous direct `Kernel` or `DbContext` injections in controllers removed.
- Build + tests green.

---
Feel free to request a concrete task breakdown or initial code moves next session.