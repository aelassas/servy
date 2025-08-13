@echo off
setlocal enabledelayedexpansion

rem === CONFIGURATION ===
set APP_NAME=Servy
set APP_VERSION=1.0.0
set TARGET_FRAMEWORK=net8.0-windows
set RUNTIME=win-x64
set CONFIGURATION=Release
set PUBLISH_DIR=publish

set PACKAGE_FOLDER=servy-%APP_VERSION%-net8.0-x64-selfcontained
set OUTPUT_ZIP=%PACKAGE_FOLDER%.zip

set OUTPUT_EXE=servy-%APP_VERSION%-net8.0-x64.exe
set OUTPUT_CLI_EXE=servy-cli-%APP_VERSION%-net8.0-x64.exe

set SRC_PROJECT=..\src\Servy\Servy.csproj
set CLI_SRC_PROJECT=..\src\Servy.CLI\Servy.CLI.csproj

rem === CLEAN OLD BUILD ===
echo Cleaning old build...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%OUTPUT_EXE%" del "%OUTPUT_EXE%"
if exist "%OUTPUT_CLI_EXE%" del "%OUTPUT_CLI_EXE%"
if exist "%PACKAGE_FOLDER%" rmdir /s /q "%PACKAGE_FOLDER%"
if exist "%OUTPUT_ZIP%" del "%OUTPUT_ZIP%"

rem === PUBLISH APP ===
echo Publishing %APP_NAME% v%APP_VERSION% (%TARGET_FRAMEWORK%, %RUNTIME%)...

rem Publish main app to PUBLISH_DIR\Servy
dotnet publish "%SRC_PROJECT%" ^
  -c %CONFIGURATION% ^
  -f %TARGET_FRAMEWORK% ^
  -r %RUNTIME% ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeAllContentForSelfExtract=true ^
  /p:PublishTrimmed=false ^
  -o "%PUBLISH_DIR%\Servy"

if errorlevel 1 (
    echo ERROR: dotnet publish for Servy failed.
    exit /b 1
)

rem Publish CLI app to PUBLISH_DIR\Servy.CLI
dotnet publish "%CLI_SRC_PROJECT%" ^
  -c %CONFIGURATION% ^
  -f %TARGET_FRAMEWORK% ^
  -r %RUNTIME% ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeAllContentForSelfExtract=true ^
  /p:PublishTrimmed=false ^
  -o "%PUBLISH_DIR%\Servy.CLI"

if errorlevel 1 (
    echo ERROR: dotnet publish for Servy.CLI failed.
    exit /b 1
)

rem === COPY EXE TO OUTPUT ===
echo Copying executables...
set EXE_PATH=%PUBLISH_DIR%\Servy\Servy.exe
if not exist "%EXE_PATH%" (
    echo ERROR: Executable not found at %EXE_PATH%
    exit /b 1
)
set CLI_EXE_PATH=%PUBLISH_DIR%\Servy.CLI\Servy.CLI.exe
if not exist "%CLI_EXE_PATH%" (
    echo ERROR: Executable not found at %CLI_EXE_PATH%
    exit /b 1
)
copy /y "%EXE_PATH%" "%OUTPUT_EXE%"
copy /y "%CLI_EXE_PATH%" "%OUTPUT_CLI_EXE%"

rem === PREPARE PACKAGE FOLDER ===
mkdir "%PACKAGE_FOLDER%"
copy /y "%OUTPUT_EXE%" "%PACKAGE_FOLDER%\"
copy /y "%OUTPUT_CLI_EXE%" "%PACKAGE_FOLDER%\"

rem === CREATE ZIP ===
echo Creating combined zip archive...
7z a -tzip "%OUTPUT_ZIP%" "%PACKAGE_FOLDER%\*" >nul

rem === CLEANUP ===
echo Cleaning up intermediate files...
rmdir /s /q "%PUBLISH_DIR%"
del /f /q "%OUTPUT_EXE%"
del /f /q "%OUTPUT_CLI_EXE%"
rmdir /s /q "%PACKAGE_FOLDER%"

echo.
echo Build complete: %OUTPUT_EXE%
echo Build complete: %OUTPUT_CLI_EXE%
echo.

endlocal
pause
