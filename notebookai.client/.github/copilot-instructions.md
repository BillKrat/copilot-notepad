# NotebookAI Client - Copilot Instructions

## Architecture Overview

This is a **hybrid Angular 19 + ASP.NET Core SPA** with Auth0 authentication and PrimeNG UI components:

- **Frontend**: Angular 19 with PrimeNG, Auth0 authentication, custom SSL setup
- **Backend**: Minimal ASP.NET Core 9.0 serving as static file host (Program.cs)
- **Build System**: Custom webpack config + npm scripts for environment switching
- **Deployment**: Web Deploy packages for IIS hosting

## Essential Developer Workflows

### Environment Management
The project uses **automated environment switching** via external config generation:
```bash
npm run env:dev    # Switches to development environment  
npm run env:prod   # Switches to production environment
npm start          # Auto-switches to dev + starts server
```

**Key insight**: Environment files are **generated** by `NotePadAI.ProjectSetup` project, not manually edited. The `switch-env.ps1` script calls an external .NET tool to generate `environment.ts` from templates.

### Development Server
```bash
npm start          # Development with auto environment switch
npm run start:prod # Production mode with SSL certificates  
```

Uses custom `scripts/start-ng.js` that detects SSL certificates and proxy configuration automatically.

### Build & Deployment
```bash
npm run build:deploy      # Production build with base-href /
npm run package:webdeploy  # Creates Web Deploy package for IIS
```

## Authentication Architecture

**Auth0 Integration** with automatic token injection:
- Configuration in `src/auth0.config.ts` with environment-specific API endpoints
- `AuthHttpInterceptor` automatically adds tokens to API calls
- API endpoints defined per environment (localhost:7280 for dev, api.global-webnet.com for prod)

## Project Conventions

### Component Structure
- **Standalone components**: New components like `WeatherComponent` use standalone pattern
- **Module-based**: Legacy components in app.module.ts with PrimeNG imports
- **Services**: Follow `ServiceResult<T>` pattern for consistent error handling (see `weather.service.ts`)

### Environment Configuration
```typescript
// environment.ts structure - DO NOT EDIT DIRECTLY
export const environment = {
  production: boolean,
  useProxy: boolean,
  apiUrl: string,
  auth0: { domain, clientId, audience }
};
```

**Critical**: Use `environment.template.ts` as reference. Actual `environment.ts` is auto-generated.

### SSL Development Setup
Custom SSL configuration for HTTPS development:
- Certificates expected in `%APPDATA%\ASP.NET\https\` (Windows) or `$HOME/.aspnet/https/` (Unix)
- Port 50012 (dev), 50013 (prod) 
- Uses `aspnetcore-https.js` for certificate setup

## Integration Points

### API Communication
- **Proxy**: `proxy.conf.json` routes `/api/*` and `/weatherforecast` to backend
- **Base URL**: Environment-specific via `environment.apiUrl`
- **Authentication**: All API calls automatically include Auth0 tokens via interceptor

### PrimeNG Integration
Extensive PrimeNG usage with custom theming:
- Modules imported in `app.module.ts`
- MessageService for notifications
- Custom styling in `styles.css`

## Key Files for Context

- `package.json` - Complex npm scripts for environment management and SSL
- `src/auth0.config.ts` - Auth0 setup with environment-specific API endpoints  
- `src/environments/environment.template.ts` - Template for generated environment files
- `scripts/start-ng.js` - Custom Angular dev server with SSL and proxy detection
- `ENVIRONMENT-SETUP.md` - Detailed environment workflow documentation
- `notebookai.client.csproj` - ASP.NET Core SPA configuration with build targets

## Debugging Notes

- **Environment issues**: Check if `switch-env.ps1` ran successfully and generated proper `environment.ts`
- **SSL errors**: Verify certificates exist in expected locations
- **Auth errors**: Confirm Auth0 config matches your tenant settings
- **Build failures**: Ensure external `NotePadAI.ProjectSetup` project is available for environment generation