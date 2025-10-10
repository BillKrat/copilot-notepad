# Copilot Notepad

> For architectural / AI design and RAG strategy details see [AI-for-Dummies](NotebookAI.Server/AI-for-Dummies.md).
>
> Quick anchors:
> [Vision](NotebookAI.Server/AI-for-Dummies.md#vision-statement-notebookai) ·
> [Current RAG Wiring](NotebookAI.Server/AI-for-Dummies.md#1-current-rag-wiring-programcs) ·
> [Gap Analysis](NotebookAI.Server/AI-for-Dummies.md#7-gap-analysis-what-to-improve-next) ·
> [Immediate Next Steps](NotebookAI.Server/AI-for-Dummies.md#8-immediate-next-steps-recommended-order) ·
> [Migration Path](NotebookAI.Server/AI-for-Dummies.md#11-migration-path-snapshot)
>
> Additional docs:
> [Server CHANGELOG](NotebookAI.Server/CHANGELOG.md) ·
> [Client CHANGELOG](notebookai.client/CHANGELOG.md) ·
> [Deployment Setup](NotebookAI.Server/DEPLOYMENT-SETUP.md) ·
> [Env Setup (Client)](notebookai.client/ENVIRONMENT-SETUP.md) ·
> [Project Setup Tool](NotebookAI.ProjectSetup/ProjectSetup.md) ·
> [WebDeploy Guide](notebookai.client/WEBDEPLOY-README.md)

## AI Architecture Snapshot (Auto‑Synced Excerpts)
<!-- BEGIN AI-EXCERPT:VISION -->
**Vision (condensed)**: Upload / manage multiple books, take study notes referencing chapter + paragraph, query own + subscribed notes, get grounded answers with citations. Extensible, pluggable storage & vector layers, clean ingestion → enrichment → retrieval → answer pipeline.
<!-- END AI-EXCERPT:VISION -->

<!-- BEGIN AI-EXCERPT:RAG-WIRING -->
**Current RAG Wiring (summary)**
```
builder.Services.AddSingleton<IBookDocumentStore, InMemoryBookDocumentStore>();
builder.Services.AddSingleton(typeof(IDocumentStore<>), typeof(InMemoryDocumentStore<>) );
builder.Services.AddSingleton<IChunker<BookDocument, BookChunk>, ParagraphChunker>();
builder.Services.AddSingleton<IVectorIndex, InMemoryVectorIndex>();
builder.Services.AddSingleton<IAdvancedRagService, HybridRagService>();
```
- In-memory stores & vector index (prototype).
- Paragraph-based chunking.
- HybridRagService orchestrates: chunk ensure -> (naive) embed+score -> prompt with citations.
<!-- END AI-EXCERPT:RAG-WIRING -->

<!-- BEGIN AI-EXCERPT:GAPS -->
**Key Gaps** (abbrev): embedding reuse, real ANN vector search, multi‑corpus (notes), access control, citations richness, caching, reranking, observability, background indexing.
<!-- END AI-EXCERPT:GAPS -->

<!-- BEGIN AI-EXCERPT:NEXT-STEPS -->
**Immediate Next Steps** (top 5):
1. Persist chunk embeddings in vector index.
2. Add notes corpus (NoteDocument + store).
3. Ownership / visibility metadata + filters.
4. Real vector backend (pgvector / Azure AI Search / Qdrant).
5. Reranking & richer citations.
<!-- END AI-EXCERPT:NEXT-STEPS -->

<!-- BEGIN AI-EXCERPT:MIGRATION -->
**Migration Path (snapshot)**
```
Prototype → +Persisted Embeddings → +Notes & ACL → +Real Vector Search → +Hybrid+Rerank → +Background Indexing & Streaming
```
<!-- END AI-EXCERPT:MIGRATION -->

---

A hybrid framework notepad application built with .NET Aspire, ASP.NET Core Web API, Angular, and a resilient FTP blue/green deployment utility. This project was generated as an exercise in AI-assisted development using GitHub Copilot.

[▶ Watch the setup video (demonstrates hybrid capabilities)](https://www.global-webnet.com/files/NotebookAI-setup.mp4)

## License

MIT License - see LICENSE file for details.

## Overview

This solution now includes:

- **NotebookAI.Server** (former ApiService) – ASP.NET Core Web API (.NET 9) with Auth0 authentication.
- **Angular Client (notebookai.client)** – SPA front-end with Auth0 integration.
- **Adventures.Shared** – Shared utilities library (FTP abstraction, logging, retry policies, connection pooling, progress reporting).
- **NotebookAI.Ftp** – Stand‑alone deployment console using blue/green (slot) strategy with automatic rollback and health checks.
- **Aspire AppHost & ServiceDefaults** – Orchestration and standardized service configuration.

## Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Angular Web   │    │  ASP.NET Core    │    │     Auth0       │
│     Client      │◄──►│    Web API       │◄──►│  Authentication │
│                 │    │                  │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                       │
         │  Health / Notes API   │
         ▼                       ▼
┌─────────────────┐    ┌──────────────────┐
│  Aspire App     │    │   In-Memory      │
│     Host        │    │   Database       │
└─────────────────┘    └──────────────────┘
         ▲
         │ Deployment (Blue/Green via FTP)
         ▼
┌─────────────────┐
│  FTP Deployment │  (NotebookAI.Ftp + Adventures.Shared)
│  Slots: base    │
│        slot1    │ (staging)
│        slot2    │ (backup/rollback)
└─────────────────┘
```

## Key Enhancements (Current State)

- **Robust FTP Abstraction**: `IFtpClientAsync` with FluentFTP implementation (`FluentFtpClientAsync`).
  - Connection lifecycle, existence checks, metadata, single & directory upload/download, recursive delete, move/rename, parallel batching, progress callbacks.
  - Polly-based retries with exponential wait (configurable pattern ready).
- **Connection Pooling**: Scoped lifetime pooling (`PooledFtpClientAsync`) with configurable `PoolSize` (default 8).
- **Dependency Injection Extensions**: `AddFtp(...)` for `IServiceCollection`, `HostApplicationBuilder`, `IHostBuilder` (and optional `WebApplicationBuilder` via symbol) with options binding.
- **Blue/Green Deployment Utility (`NotebookAI.Ftp`)**:
  - Uploads new build to staging slot (slot1).
  - Validates staging (ensures files exist) before touching production.
  - Backs up current production to backup slot (slot2) with rollback if partial moves fail.
  - Promotes staging to production.
  - Post-deploy health checks (configurable required files) – automatic rollback if failed.
  - Cleans staging only after successful health & promotion.
  - Progress logging via `ILogger` + `IProgress<FtpProgress>`.
  - Parallel-friendly design (ready to leverage explicit parallel file upload if needed).
- **Automatic Rollback**: Restores contents from backup slot if validation or health fails.
- **Progress & Logging**: Unified structured logging (can replace logger in DI to adapt output, e.g., console UI or telemetry).
- **Configuration Driven**: FTP credentials & remote folder from user secrets / config section `Ftp`.

## FTP Configuration

Example `UserSecrets` (already supported):
```
"Ftp": {
  "host": "your-ftp-host",
  "remote-folder": "/global",
  "port": 21,
  "username": "user",
  "password": "secret",
  "site-url": "https://www.example.com"
}
```
Optional deployment settings:
```
"Deployment": {
  "Parallelism": 8,
  "HealthCheck": {
    "Paths": ["index.html", "assets/app.js"]
  }
}
```
If `Deployment:HealthCheck:Paths` is omitted, the deployment tool defaults to verifying `index.html` exists after promotion.

## Running the Deployment Utility

```
# From solution root
 dotnet run --project NotebookAI.Ftp <optional-path-to-built-web-dist>
```
If no path is supplied, it attempts to locate `notebookai.client/dist/notebookai.client` automatically.

Deployment phases (all safe & reversible):
1. Prepare folders (base, slot1, slot2).
2. Clean staging slot only.
3. Upload new artifacts to slot1 with progress.
4. Validate staging.
5. Backup production to slot2 (rollback on failure).
6. Promote slot1 → base (rollback on failure mid-move).
7. Health checks (rollback to slot2 if any fail).
8. Clean staging slot.

## Features (Application Layer)

- **Secure Authentication** (Auth0 JWT)
- **Notes CRUD with User Isolation**
- **Global Exception Handling & Structured Logging**
- **Health Monitoring** (API + Deployment health checks)
- **Input Validation**
- **AI-Ready Services** (extensible for future integrations)
- **Modern Angular Frontend**
- **Aspire Orchestration** (service defaults, local diagnostics)

## Prerequisites

- .NET 9.0 SDK (solution targets net9 + net8 where noted)
- Node.js 18+
- Auth0 account (for auth)

## Getting Started (Core App)

Clone & build:
```
git clone https://github.com/BillKrat/copilot-notepad.git
cd copilot-notepad
```

Configure Auth0 (as previously documented) and set `UserSecrets` for FTP & Auth0.

### Run with Aspire
```
dotnet workload install aspire
(dotnet build)
dotnet run --project NotebookAI.AppHost
```

### Development Mode
```
# API (Server)
dotnet run --project NotebookAI.Server
# Frontend
cd notebookai.client
npm install
npm start
```

## Health Checks & Rollback Logic

- Deployment health checks are file-based (existence). Extend easily to HTTP probes or content validation by adding logic in `RunHealthChecksAsync`.
- Rollback strategy preserves previous production state until new deployment + health succeed.

## Extending the FTP Layer

You can further customize by:
- Adding named FTP clients (multiple endpoints) via distinct option registrations.
- Injecting custom progress loggers (implement `IProgress<FtpProgress>`).
- Adding bandwidth throttling or checksum verification (`FtpVerify` modes) where needed.
- Enhancing health checks: e.g., HTTP GET to `site-url` validating status 200.

## API Endpoints (Notes Service)

- GET /api/notes
- GET /api/notes/{id}
- POST /api/notes
- PUT /api/notes/{id}
- DELETE /api/notes/{id}
(All require valid Auth0 JWT.)

## Project Structure (Updated)

```
copilot-notepad/
├── NotebookAI.AppHost/              # Aspire orchestration
├── NotebookAI.ServiceDefaults/      # Shared service defaults
├── NotebookAI.Server/               # ASP.NET Core Web API
│   ├── Program.cs
│   └── ...
├── Adventures.Shared/               # Shared libs (FTP abstraction, pooling, retry, logging)
├── NotebookAI.Ftp/                  # Deployment console (blue/green + rollback)
├── notebookai.client/               # Angular client
└── CopilotNotepad.sln               # Solution file
```

## Roadmap Ideas

- HTTP-based post-deploy health checks
- Content hash verification during upload
- Incremental (delta) uploads
- CDN cache purge integration
- Automated test suite for deployment logic

## Security

Never commit real secrets. Use `dotnet user-secrets` for local dev. Rotate credentials if exposed.

## Acknowledgements

- FluentFTP
- Polly
- Auth0
- .NET Aspire Team

---
**Generated & iteratively evolved with GitHub Copilot.**
