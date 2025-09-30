# Production Deployment Setup

## Environment Configuration

### Local Development
- Uses **User Secrets** (already configured)
- Right-click project → "Manage User Secrets"

### Production Deployment

#### Option 1: Environment Variables (Recommended)
Set these environment variables in your hosting environment:

Auth0__Domain=your-auth0-domain.auth0.com 
Auth0__ClientId=your-auth0-client-id 
Auth0__Audience=https://your-api-identifier

#### Option 2: appsettings.Production.json (Alternative)
1. Copy `appsettings.Production.template.json` to `appsettings.Production.json`
2. Update with your production values
3. Deploy the file directly to your server (not committed to git)

## Deployment Environments

### Azure App Service
- Add settings in **Configuration → Application Settings**
- Format: `Auth0:Domain`, `Auth0:ClientId`, `Auth0:Audience`

### Docker

ENV Auth0__Domain=your-auth0-domain.auth0.com 
ENV Auth0__ClientId=your-auth0-client-id
ENV Auth0__Audience=https://your-api-identifier

### IIS/Windows Server
- Set environment variables in IIS Application Settings
- Or deploy `appsettings.Production.json` directly to server

## Security Notes
- ✅ `appsettings.Production.json` is excluded from git
- ✅ Template files provide structure without secrets
- ✅ User secrets for local development
- ✅ Environment variables for production