# Simple PowerShell script to create Web Deploy package
# Handles encoding issues and provides better error output

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

try {
    if ($Verbose) {
        Write-Host "Starting Web Deploy package creation..." -ForegroundColor Green
    }

    if ($Verbose) { Write-Host "Generating production configuration..." -ForegroundColor Yellow }
    dotnet run --project "..\NotePadAI.ProjectSetup\NotePadAI.ProjectSetup.csproj" -- --env prod --client-root "."
    if ($LASTEXITCODE -ne 0) { throw "Failed to generate configuration files" }

    if ($Verbose) { Write-Host "Cleaning previous builds..." -ForegroundColor Yellow }
    # Clean previous builds
    if (Test-Path "dist") {
        Remove-Item -Recurse -Force "dist"
        if ($Verbose) { Write-Host "Cleaned previous builds" -ForegroundColor Yellow }
    }

    if ($Verbose) { Write-Host "Building Angular app for production..." -ForegroundColor Yellow }
    # Build the Angular app
    $buildProcess = Start-Process -FilePath "npm" -ArgumentList "run", "build:prod" -NoNewWindow -Wait -PassThru
    if ($buildProcess.ExitCode -ne 0) {
        throw "Build failed with exit code: $($buildProcess.ExitCode)"
    }

    # Create Web Deploy package
    $sourceFolder = ".\dist\notebookai.client"
    $zipFile = ".\dist\notebookai-webdeploy.zip"

    if (-not (Test-Path $sourceFolder)) {
        throw "Build output folder not found: $sourceFolder"
    }

    if (Test-Path $zipFile) {
        Remove-Item $zipFile -Force
    }

    # Create the zip file
    Compress-Archive -Path "$sourceFolder\*" -DestinationPath $zipFile -Force

    if (Test-Path $zipFile) {
        $fileSize = (Get-Item $zipFile).Length / 1MB
        Write-Host "SUCCESS: Web Deploy package created" -ForegroundColor Green
        Write-Host "Package: $zipFile" -ForegroundColor Cyan
        Write-Host "Size: $($fileSize.ToString('F2')) MB" -ForegroundColor Cyan
    } else {
        throw "Failed to create zip file"
    }

    if ($Verbose) { Write-Host "Restoring development configuration..." -ForegroundColor Yellow }
    dotnet run --project "..\NotePadAI.ProjectSetup\NotePadAI.ProjectSetup.csproj" -- --env dev --client-root "."

} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
