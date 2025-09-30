@echo off
REM Batch file to create Web Deploy package for NotebookAI Angular app
setlocal EnableDelayedExpansion

echo [INFO] Creating Web Deploy package for NotebookAI...

REM Ensure production configuration is generated from user-secrets
call configure-for.bat prod
if errorlevel 1 exit /b 1

echo [INFO] Cleaning previous builds...
if exist "dist" rmdir /s /q "dist"

echo [INFO] Building Angular app for production...
call npm run build:prod
if !ERRORLEVEL! neq 0 (
    echo [ERROR] Build failed!
    exit /b 1
)

if not exist "dist\notebookai.client" (
    echo [ERROR] Build output folder not found: dist\notebookai.client
    exit /b 1
)

echo [INFO] Creating Web Deploy package...
set "sourceFolder=dist\notebookai.client"
set "zipFile=dist\notebookai-webdeploy.zip"
if exist "%zipFile%" del "%zipFile%"
powershell -Command "Compress-Archive -Path '%sourceFolder%\*' -DestinationPath '%zipFile%' -Force"

if exist "%zipFile%" (
    echo [SUCCESS] Web Deploy package created successfully!
    echo [INFO] Package location: %zipFile%
) else (
    echo [ERROR] Failed to create Web Deploy package!
    exit /b 1
)

echo [INFO] Restoring development environment files...
call configure-for.bat dev

echo [SUCCESS] Ready for Web Deploy!
