
# NotebookAI Web Deploy Package

This document explains how to create and deploy the NotebookAI Angular application using Web Deploy.


## How to invoke the Web Deploy Package Creation

# Option 1: Via Launch Configuration
Go to Run and Debug panel (Ctrl+Shift+D / Cmd+Shift+D on Mac)
Select "Create Web Deploy Package" from the dropdown
Click the green play button or press F5

# Option 2: Via Tasks Panel
Open Command Palette (Ctrl+Shift+P / Cmd+Shift+P on Mac)
Type "Tasks: Run Task"
Select "npm: package:webdeploy-bat"

# Option 3: Via Command Palette
Open Command Palette (Ctrl+Shift+P / Cmd+Shift+P on Mac)
Type "Tasks: Run Build Task"
Select the Web Deploy package task


## Quick Start

### 1. Create Web Deploy Package

```bash
# Method 1: Using npm script (PowerShell)
npm run package:webdeploy

# Method 2: Using npm script (Batch file - handles encoding better)
npm run package:webdeploy-bat

# Method 3: Using PowerShell script directly
.\create-webdeploy-simple.ps1 -Verbose

# Method 4: Using Batch file directly  
.\create-webdeploy-package.bat
```

### 2. Deploy the Package

The package will be created at: `dist/notebookai-webdeploy.zip`

## Deployment Options

### Option A: Visual Studio Web Deploy
1. Open Visual Studio
2. Right-click on web project → Publish
3. Choose "Import Profile"
4. Select the generated `.zip` file
5. Configure target server settings
6. Click "Publish"

### Option B: IIS Manager
1. Open IIS Manager
2. Right-click on "Sites" → "Deploy" → "Import Application"
3. Select the `notebookai-webdeploy.zip` file
4. Follow the wizard to complete deployment

### Option C: Azure App Service
1. Go to Azure Portal → App Service
2. Navigate to "Deployment Center"
3. Choose "ZIP Deploy"
4. Upload the `notebookai-webdeploy.zip` file

### Option D: Command Line (MSDeploy)
```cmd
msdeploy.exe -source:package="dist/notebookai-webdeploy.zip" -dest:auto,computerName="https://yourserver:8172/MSDeploy.axd",userName="username",password="password",authType="Basic" -allowUntrusted -verb:sync
```

## Package Contents

The Web Deploy package includes:
- ✅ **Angular build files** (HTML, CSS, JS)
- ✅ **web.config** for IIS URL rewriting
- ✅ **Static assets** (images, fonts, etc.)
- ✅ **Security headers** configuration
- ✅ **Compression** settings
- ✅ **Cache control** for performance

## Environment Configuration

### Production Environment Variables
Update your production environment with:

```bash
# API Configuration
API_URL=https://your-production-api.com
USE_PROXY=false

# Auth0 Configuration  
AUTH0_DOMAIN=your-auth0-domain.auth0.com
AUTH0_CLIENT_ID=your-production-client-id
AUTH0_AUDIENCE=https://your-auth0-domain.auth0.com/api/v2/

# Environment
ENVIRONMENT=production
DEBUG_MODE=false
```

### IIS Configuration Requirements

Ensure your IIS server has:
- ✅ **URL Rewrite Module** installed
- ✅ **Static Content** feature enabled
- ✅ **HTTP Compression** enabled
- ✅ **.NET Framework** or .NET Core hosting bundle

## Troubleshooting

### Common Issues:

1. **Encoding issues in PowerShell (Unicode characters showing as ���)**
   - Use the batch file version: `npm run package:webdeploy-bat`
   - Or run directly: `.\create-webdeploy-package.bat`
   - Ensure terminal encoding is set to UTF-8

2. **PowerShell execution policy errors**
   - Run: `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`
   - Or use the batch file alternative

3. **404 on page refresh**
   - Ensure URL Rewrite module is installed
   - Verify web.config is included in deployment

2. **API calls failing**
   - Check API_URL environment variable
   - Verify CORS settings on API server
   - Update Auth0 allowed origins

3. **Assets not loading**
   - Check base-href setting in build
   - Verify static content serving is enabled

### Build Commands Reference:

```bash
# Development build
npm run build

# Production build
npm run build:prod

# Production build with custom base href
npm run build:deploy

# Create Web Deploy package (PowerShell)
npm run package:webdeploy

# Create Web Deploy package (Batch file - better encoding support)
npm run package:webdeploy-bat
```

## Security Considerations

The web.config includes:
- 🔒 **Security headers** (XSS protection, content type sniffing prevention)
- 🔒 **HSTS** (HTTP Strict Transport Security)
- 🔒 **Frame options** to prevent clickjacking
- 🔒 **Hidden segments** to protect config files

## Performance Optimizations

- 🚀 **Gzip compression** enabled
- 🚀 **Static asset caching** (1 year)
- 🚀 **Minified and bundled** JavaScript/CSS
- 🚀 **Tree-shaken** dependencies

---

**Need help?** Check the deployment logs in IIS or contact your system administrator.
