@echo off
setlocal EnableExtensions
if "%~1"=="" goto usage
set "ENV=%~1"
if /I not "%ENV%"=="dev" if /I not "%ENV%"=="prod" goto usage

echo [INFO] Generating configuration for %ENV%...
dotnet run --project "..\NotePadAI.ProjectSetup\NotePadAI.ProjectSetup.csproj" -- --env %ENV% --client-root "."
if errorlevel 1 (
  echo [ERROR] Failed to generate configuration files.
  exit /b 1
)
echo [SUCCESS] Configuration generated for %ENV%.
exit /b 0

:usage
echo Usage: configure-for.bat [dev^|prod]
exit /b 1
