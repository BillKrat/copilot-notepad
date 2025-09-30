param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("dev","prod")]
    [string]$Environment
)

Write-Host "Generating configuration for $Environment..." -ForegroundColor Yellow
dotnet run --project "..\NotePadAI.ProjectSetup\NotePadAI.ProjectSetup.csproj" -- --env $Environment --client-root "."
if ($LASTEXITCODE -ne 0) {
  Write-Host "ERROR: Failed to generate configuration files." -ForegroundColor Red
  exit 1
}
Write-Host "✅ Configuration generated for $Environment" -ForegroundColor Green
Write-Host "Current configuration:" -ForegroundColor Cyan
Get-Content ".env" | Select-String "ENVIRONMENT=|API_URL="
