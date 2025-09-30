# PowerShell script to create Web Deploy package for NotebookAI Angular app
# Run this script from the project root directory

Write-Host "[*] Creating Web Deploy package for NotebookAI..." -ForegroundColor Green

# Ensure production configuration is generated from user-secrets
Write-Host "[*] Generating production configuration..." -ForegroundColor Yellow
dotnet run --project "..\NotePadAI.ProjectSetup\NotePadAI.ProjectSetup.csproj" -- --env prod --client-root "."
if ($LASTEXITCODE -ne 0) { Write-Host "[ERROR] Generation failed!" -ForegroundColor Red; exit 1 }

Write-Host "[*] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path "dist") { Remove-Item -Recurse -Force "dist" }

Write-Host "[*] Building Angular app for production..." -ForegroundColor Yellow
npm run build:prod
if ($LASTEXITCODE -ne 0) { Write-Host "[ERROR] Build failed!" -ForegroundColor Red; exit 1 }

Write-Host "[*] Creating Web Deploy package..." -ForegroundColor Yellow
$sourceFolder = ".\dist\notebookai.client"
$zipFile = ".\dist\notebookai-webdeploy.zip"
if (Test-Path $zipFile) { Remove-Item $zipFile -Force }
Compress-Archive -Path "$sourceFolder\*" -DestinationPath $zipFile -Force

Write-Host "[SUCCESS] Web Deploy package created successfully!" -ForegroundColor Green
Write-Host "[INFO] Package location: $zipFile" -ForegroundColor Cyan

# Restore dev environment files for local work
Write-Host "[*] Restoring development configuration..." -ForegroundColor Yellow
dotnet run --project "..\NotePadAI.ProjectSetup\NotePadAI.ProjectSetup.csproj" -- --env dev --client-root "."
