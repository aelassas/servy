@echo off
setlocal enabledelayedexpansion

rem === CONFIGURATION ===
set APP_NAME=Servy
set APP_VERSION=1.0.0
set TARGET_FRAMEWORK=net8.0-windows
set RUNTIME=win-x64
set CONFIGURATION=Release
set PUBLISH_DIR=publish
set OUTPUT_EXE=servy-%APP_VERSION%-net8.0-x64-portable.exe
set OUTPUT_CLI_EXE=servy-%APP_VERSION%-net8.0-x64-cli-portable.exe
set SRC_PROJECT=..\src\Servy\Servy.csproj
set CLI_SRC_PROJECT=..\src\Servy.CLI\Servy.CLI.csproj

rem === CLEAN OLD BUILD ===
echo Cleaning old build...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%OUTPUT_EXE%" del "%OUTPUT_EXE%"

rem === PUBLISH APP ===
echo Publishing %APP_NAME% v%APP_VERSION% (%TARGET_FRAMEWORK%, %RUNTIME%)...
dotnet publish "%SRC_PROJECT%" ^
  -c %CONFIGURATION% ^
  -f %TARGET_FRAMEWORK% ^
  -r %RUNTIME% ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeAllContentForSelfExtract=true ^
  /p:PublishTrimmed=false ^
  -o "%PUBLISH_DIR%"

if errorlevel 1 (
    echo ERROR: dotnet publish failed.
    exit /b 1
)

dotnet publish "%CLI_SRC_PROJECT%" ^
  -c %CONFIGURATION% ^
  -f %TARGET_FRAMEWORK% ^
  -r %RUNTIME% ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeAllContentForSelfExtract=true ^
  /p:PublishTrimmed=false ^
  -o "%PUBLISH_DIR%"

if errorlevel 1 (
    echo ERROR: dotnet publish failed.
    exit /b 1
)

rem === COPY EXE TO OUTPUT ===
echo Copying executables...
rem Find the main exe in publish folder (usually Servy.exe)
set EXE_PATH=%PUBLISH_DIR%\Servy.exe
if not exist "%EXE_PATH%" (
    echo ERROR: Executable not found at %EXE_PATH%
    exit /b 1
)
set CLI_EXE_PATH=%PUBLISH_DIR%\Servy.CLI.exe
if not exist "%CLI_EXE_PATH%" (
    echo ERROR: Executable not found at %EXE_PATH%
    exit /b 1
)
copy /y "%EXE_PATH%" "%OUTPUT_EXE%"
copy /y "%CLI_EXE_PATH%" "%OUTPUT_CLI_EXE%"

rem === CLEANUP ===
echo Deleting publish folder...
rmdir /s /q "%PUBLISH_DIR%"

echo.
echo Build complete: %OUTPUT_EXE%
echo Build complete: %OUTPUT_CLI_EXE%
echo.

endlocal
pause
