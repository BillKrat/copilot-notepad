# Copilot Notepad

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

## Contributing

PRs welcome. Please open an issue first for major proposals.

## Security

Never commit real secrets. Use `dotnet user-secrets` for local dev. Rotate credentials if exposed.

## Acknowledgements

- FluentFTP
- Polly
- Auth0
- .NET Aspire Team

---
**Generated & iteratively evolved with GitHub Copilot.**
