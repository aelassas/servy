@echo off
setlocal enabledelayedexpansion

rem === CONFIGURATION ===
set APP_NAME=Servy
set APP_VERSION=1.0.0
set TARGET_FRAMEWORK=net8.0-windows
set RUNTIME=win-x64
set CONFIGURATION=Release

set APP_PUBLISH_DIR=publish-fd
set CLI_PUBLISH_DIR=publish-cli-fd

set OUTPUT_DIR=servy-%APP_VERSION%-net8.0-x64-frameworkdependent
set OUTPUT_ZIP=%OUTPUT_DIR%.zip

set APP_OUTPUT_DIR=servy-app
set CLI_OUTPUT_DIR=servy-cli

set SRC_PROJECT=..\src\Servy\Servy.csproj
set CLI_SRC_PROJECT=..\src\Servy.CLI\Servy.CLI.csproj

echo Cleaning old build artifacts...
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
if exist "%OUTPUT_ZIP%" del "%OUTPUT_ZIP%"
if exist "%APP_PUBLISH_DIR%" rmdir /s /q "%APP_PUBLISH_DIR%"
if exist "%CLI_PUBLISH_DIR%" rmdir /s /q "%CLI_PUBLISH_DIR%"

rem === PUBLISH APP ===
echo Publishing %APP_NAME%...
dotnet publish "%SRC_PROJECT%" ^
  -c %CONFIGURATION% ^
  -f %TARGET_FRAMEWORK% ^
  -r %RUNTIME% ^
  --self-contained false ^
  /p:PublishSingleFile=false ^
  /p:IncludeAllContentForSelfExtract=true ^
  /p:PublishTrimmed=false ^
  -o "%APP_PUBLISH_DIR%"

if errorlevel 1 (
    echo ERROR: dotnet publish failed for %APP_NAME%.
    exit /b 1
)

rem === PUBLISH CLI ===
echo Publishing %APP_NAME% CLI...
dotnet publish "%CLI_SRC_PROJECT%" ^
  -c %CONFIGURATION% ^
  -f %TARGET_FRAMEWORK% ^
  -r %RUNTIME% ^
  --self-contained false ^
  /p:PublishSingleFile=false ^
  /p:IncludeAllContentForSelfExtract=true ^
  /p:PublishTrimmed=false ^
  -o "%CLI_PUBLISH_DIR%"

if errorlevel 1 (
    echo ERROR: dotnet publish failed for %APP_NAME% CLI.
    exit /b 1
)

ren "%CLI_PUBLISH_DIR%\Servy.CLI.exe" "servy-cli.exe"

rem === COPY PUBLISH OUTPUTS TO COMBINED FOLDER ===
echo Copying publish outputs to combined folder...
mkdir "%OUTPUT_DIR%\%APP_OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%\%CLI_OUTPUT_DIR%"

xcopy "%APP_PUBLISH_DIR%\*" "%OUTPUT_DIR%\%APP_OUTPUT_DIR%\" /S /Y /I
xcopy "%CLI_PUBLISH_DIR%\*" "%OUTPUT_DIR%\%CLI_OUTPUT_DIR%\" /S /Y /I

rem === CREATE ZIP ===
echo Creating combined zip archive...
7z a -tzip "%OUTPUT_ZIP%" "%OUTPUT_DIR%\*" >nul

rem === CLEANUP ===
echo Cleaning up temporary publish folders...
rmdir /s /q "%APP_PUBLISH_DIR%"
rmdir /s /q "%CLI_PUBLISH_DIR%"
rmdir /s /q "%OUTPUT_DIR%"

echo.
echo Build and packaging complete:
echo   %OUTPUT_ZIP%
echo.

endlocal
pause
