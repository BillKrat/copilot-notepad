@echo off
if "%~1"=="" (
  echo Usage: switch-env.bat [dev^|prod]
  exit /b 1
)
call configure-for.bat %1
type .env | findstr "ENVIRONMENT= API_URL="
