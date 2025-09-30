# ProjectSetup Tool Guide

Centralize all environment configuration in one place. The `NotePadAI.ProjectSetup` console app reads developer-local User Secrets and generates the client and server configuration files required by the solution—without committing secrets.

What it generates
- Client (Angular: `notebookai.client`)
  - `.env.development`
  - `.env.production`
  - `.env` (active, based on selected env)
  - `src/environments/environment.ts`
  - `src/environments/environment.prod.ts`
  - Ensures `.gitignore` excludes the generated files above
- Server (API: e.g., `NotebookAI.Server`)
  - `appsettings.Development.json`
  - `appsettings.Production.json`
  - If Auth0 sync is not enabled: `auth0-required-urls.json` (helper output)

Secrets remain only in User Secrets. Scripts call this tool instead of embedding credentials.

---

## Prerequisites

- .NET SDK 9
- Node.js + npm (for Angular client)
- Auth0 tenant with:
  - SPA Application (ClientId you use in Angular)
  - Optional: M2M Application with permission `update:clients` for automated Auth0 sync

---

## First-Time Setup

1) Create User Secrets
- In Visual Studio 2022: right-click the `NotePadAI.ProjectSetup` project → Manage User Secrets.
- Paste a secrets schema (replace placeholders with your values):
```
{
	"Environments": {
		"Dev": {
			"clientUrl": "http://localhost:4200",
			"apiUrl": "https://localhost:7280",
			"production": false,
			"useProxy": false,
			"auth0": {
				"domain": "your-tenant.us.auth0.com",
				"clientId": "SPA_CLIENT_ID",
				"audience": "https://your-tenant.us.auth0.com/api/v2/"
			}
		},
		"Prod": {
			"clientUrl": "https://app.example.com",
			"apiUrl": "https://api.example.com",
			"production": true,
			"useProxy": false,
			"auth0": {
				"domain": "your-tenant.us.auth0.com",
				"clientId": "SPA_CLIENT_ID",
				"audience": "https://your-tenant.us.auth0.com/api/v2/"
			}
		}
	}
}
```
// Optional: required only if you want automatic Auth0 sync "Auth0Management": 
```
{
	"domain": "your-tenant.us.auth0.com",
	"clientId": "M2M_CLIENT_ID",
	"clientSecret": "M2M_CLIENT_SECRET",
	"targetClientId": "SPA_CLIENT_ID"
}
```
2) Stop tracking generated files (one-time cleanup, if they were committed earlier)

`git rm --cached notebookai.client/.env` 

`notebookai.client/.env.development`<br>
`notebookai.client/.env.production `<br>
`notebookai.client/src/environments/environment.ts `<br>
`notebookai.client/src/environments/environment.prod.ts git commit -m "Remove generated config files from source control"`

3) Confirm ignores
- The tool adds `.gitignore` entries in `notebookai.client` automatically.
- For server, ensure `appsettings.Production.json` stays out of git if that’s your policy.

---

## Usage

Run from the repo root (adjust paths if your folders differ).

Generate development configuration

dotnet run --project .\NotePadAI.ProjectSetup\NotePadAI.ProjectSetup.csproj -- --env dev --client-root ".\notebookai.client" --server-root ".\NotebookAI.Server" -v

Generate production configuration

dotnet run --project .\NotePadAI.ProjectSetup\NotePadAI.ProjectSetup.csproj -- --env prod --client-root ".\notebookai.client" --server-root ".\NotebookAI.Server" -v

What gets created/updated
- Client: `.env*`, `environment*.ts`, and `.gitignore` entries
- Server: `appsettings.Development.json` and `appsettings.Production.json` with non-secret Auth0 settings (Domain, ClientId, Audience)

Idempotent: safe to run multiple times.

Defaults (if flags omitted)
- `--env`: `dev`
- `--client-root`: `..\notebookai.client`
- `--server-root`: `..\NotebookAI.Server`

---

## Auth0 Sync (optional)

If you provide `Auth0Management` in User Secrets, the tool can update your Auth0 SPA application settings:

- callbacks
- allowed_logout_urls
- web_origins
- allowed_origins

These are computed from your `clientUrl`, `apiUrl`, and standard Swagger OAuth redirect:
- Swagger redirects: `{apiUrl}/swagger/oauth2-redirect.html` (Dev/Prod)
- SPA origin/callback: `{clientUrl}` (Dev/Prod)

Run with sync enabled

dotnet run --project .\NotePadAI.ProjectSetup\NotePadAI.ProjectSetup.csproj -- --env prod --client-root ".\notebookai.client" --server-root ".\NotebookAI.Server" --sync-auth0 -v

Requirements
- M2M app must have `update:clients` permission for the Management API.
- If `--sync-auth0` is not used or credentials are missing, the tool writes `auth0-required-urls.json` (for manual copy/paste into Auth0).

---

## Integration with Scripts

Batch/PowerShell wrappers call the ProjectSetup tool so no secrets live in scripts:
- `notebookai.client\configure-for.bat [dev|prod]`
- `notebookai.client\switch-env.bat [dev|prod]`
- `notebookai.client\switch-env.ps1 -Environment [dev|prod]`
- `notebookai.client\create-webdeploy-package.(bat|ps1)`
- `notebookai.client\create-webdeploy-simple.ps1`

These scripts:
- Generate the correct environment via the console app
- Build/package the Angular app
- Optionally restore dev config after packaging

---

## Command Reference

- `--env [dev|prod]` Select active environment for `.env` and derived files
- `--client-root <path>` Path to Angular client root (contains `.env` and `src/environments`)
- `--server-root <path>` Path to server root (contains `appsettings.*.json`)
- `--sync-auth0` Enable Auth0 Management API sync (requires `Auth0Management` in User Secrets)
- `-v|--verbose` Verbose output
- `-h|--help` Show usage

---

## Troubleshooting

- Missing keys error
  - Ensure required keys exist in User Secrets:
    - `Environments:Dev|Prod:apiUrl`
    - `Environments:Dev|Prod:auth0:domain|clientId|audience`
- Wrong paths
  - Provide explicit `--client-root` and `--server-root` matching your repo layout.
- Auth0 sync fails
  - Verify M2M credentials and `update:clients` permission.
  - Check tenant domain and SPA client ID (`targetClientId`).

---

## Recommended Workflows

- Development
  - `npm run start` (client)
  - Use `switch-env` scripts or run the tool directly with `--env dev`.
- Production build/package
  - `notebookai.client\create-webdeploy-package.bat` or `.ps1` (these call the ProjectSetup tool for `prod` and restore `dev` afterward).

Single source of truth: edit only User Secrets; run the tool to regenerate all dependent files.




