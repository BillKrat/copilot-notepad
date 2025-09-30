# Copilot Notepad

A hybrid framework notepad application built with .NET Aspire, ASP.NET Core Web API, and Angular with Auth0 authentication.

## Overview

This project demonstrates a modern full-stack application architecture using:

- **.NET Aspire** - For application orchestration and service discovery
- **ASP.NET Core Web API** - Backend service with JWT authentication
- **Angular** - Frontend client application with Auth0 integration
- **Auth0** - Authentication and authorization provider
- **Entity Framework Core** - In-memory database for notes storage

## Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Angular Web   │    │  ASP.NET Core    │    │     Auth0       │
│     Client      │◄──►│    Web API       │◄──►│  Authentication │
│                 │    │                  │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                       │
         │                       │
         ▼                       ▼
┌─────────────────┐    ┌──────────────────┐
│  Aspire App     │    │   In-Memory      │
│     Host        │    │   Database       │
│  (Orchestrator) │    │                  │
└─────────────────┘    └──────────────────┘
```

## Features

- **Secure Authentication**: Auth0 integration with JWT tokens
- **Notes Management**: Create, read, update, and delete notes
- **User Isolation**: Each user can only access their own notes
- **Real-time UI**: Modern Angular interface with responsive design
- **Service Discovery**: Aspire handles service orchestration
- **Development Experience**: Hot reload and integrated debugging
- **Health Monitoring**: Built-in health checks and monitoring
- **Error Handling**: Global exception handling with structured logging
- **Input Validation**: Comprehensive request validation with detailed error messages
- **AI Integration Ready**: Service abstractions prepared for OpenAI integration

## Prerequisites

- .NET 9.0 SDK or later
- Node.js 18+ and npm
- Auth0 account (for authentication setup)

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/BillKrat/copilot-notepad.git
cd copilot-notepad
```

### 2. Configure Auth0

1. Create an Auth0 application at [auth0.com](https://auth0.com)
2. Set up your Auth0 application:
   - Application Type: Single Page Application (for Angular)
   - Allowed Callback URLs: `http://localhost:4200`
   - Allowed Logout URLs: `http://localhost:4200`
   - Allowed Web Origins: `http://localhost:4200`
3. Create an API in Auth0:
   - Name: Copilot Notepad API
   - Identifier: `https://copilot-notepad-api`
4. Update the configuration files:

**Backend (CopilotNotepad.ApiService/appsettings.json):**
```json
{
  "Auth0": {
    "Domain": "https://your-auth0-domain.auth0.com",
    "Audience": "https://copilot-notepad-api"
  }
}
```

**Frontend (CopilotNotepad.Web/src/app/auth0.config.ts):**
```typescript
export const AUTH0_CONFIG: Auth0Config = {
  domain: 'your-auth0-domain.auth0.com',
  clientId: 'your-auth0-client-id',
  audience: 'https://copilot-notepad-api',
  redirectUri: window.location.origin
};
```

> **Note:** For development, you can create placeholder values, but Auth0 authentication will be required for the app to fully function.

### 3. Run the Application

**Option 1: Using Aspire (Recommended for Production)**
```bash
# Install .NET Aspire workload (if not already installed)
dotnet workload install aspire

# Build the solution
dotnet build

# Run the Aspire application
dotnet run --project CopilotNotepad.AppHost
```

The Aspire dashboard will open in your browser, showing the API Service running.

**Option 2: Development Mode (Recommended for Development)**

Run the API and Angular app separately for the best development experience:

```bash
# Terminal 1: Run the API service
dotnet run --project CopilotNotepad.ApiService

# Terminal 2: Run the Angular app
cd CopilotNotepad.Web
npm start
```

The API will run on `https://localhost:7001` and the Angular app on `http://localhost:4200`.

### 4. Development

For development with hot reload:

**Backend:**
```bash
dotnet watch --project CopilotNotepad.ApiService
```

**Frontend:**
```bash
cd CopilotNotepad.Web
npm start
```

## API Endpoints

The API provides the following endpoints (all require authentication):

- `GET /api/notes` - Get all notes for the authenticated user
- `GET /api/notes/{id}` - Get a specific note
- `POST /api/notes` - Create a new note
- `PUT /api/notes/{id}` - Update an existing note
- `DELETE /api/notes/{id}` - Delete a note

## Technology Stack

### Backend
- .NET 9.0
- ASP.NET Core Web API
- Entity Framework Core (In-Memory)
- Microsoft.AspNetCore.Authentication.JwtBearer
- .NET Aspire 9.5
- Global exception handling
- Structured logging with JSON console
- Health checks endpoint

### Frontend
- Angular 20+
- TypeScript
- SCSS
- Auth0 SPA SDK
- RxJS

### Authentication
- Auth0
- JWT Tokens
- CORS enabled for local development

## Project Structure

```
copilot-notepad/
├── CopilotNotepad.AppHost/           # Aspire orchestration
├── CopilotNotepad.ServiceDefaults/   # Shared service configurations
├── CopilotNotepad.ApiService/        # ASP.NET Core Web API
│   ├── Models/                       # Data models
│   ├── Data/                         # Entity Framework context
│   └── Program.cs                    # API configuration
├── CopilotNotepad.Web/              # Angular application
│   ├── src/app/                     # Angular components and services
│   ├── src/app/models/              # TypeScript models
│   └── package.json                 # NPM dependencies
└── CopilotNotepad.sln               # Solution file
```

## Contributing

This project was generated as an exercise in AI-assisted development using GitHub Copilot.

## License

MIT License - see LICENSE file for details.