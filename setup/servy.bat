@echo off
setlocal enabledelayedexpansion

rem === CONFIGURATION ===
set APP_NAME=servy
set APP_VERSION=1.0.0
set BUILD_CONFIG=Release
set PLATFORM=x64
set FRAMEWORK=net48
set PACKAGE_FOLDER=%APP_NAME%-%APP_VERSION%-%FRAMEWORK%-%PLATFORM%-portable
set OUTPUT_ZIP=%PACKAGE_FOLDER%.zip
set BUILD_OUTPUT_DIR=..\src\Servy\bin\%BUILD_CONFIG%

rem === CLEAN OLD ARTIFACTS ===
echo Cleaning old build artifacts...
if exist "%PACKAGE_FOLDER%" rmdir /s /q "%PACKAGE_FOLDER%"
if exist "%OUTPUT_ZIP%" del "%OUTPUT_ZIP%"

rem === COPY BUILD FILES TO PACKAGE DIRECTORY ===
echo Preparing package files...
mkdir "%PACKAGE_FOLDER%"
xcopy "%BUILD_OUTPUT_DIR%\%APP_NAME%.exe" "%PACKAGE_FOLDER%\" /Y
xcopy "%BUILD_OUTPUT_DIR%\Servy.Core.dll" "%PACKAGE_FOLDER%\" /Y
xcopy "%BUILD_OUTPUT_DIR%\Resources\*" "%PACKAGE_FOLDER%\Resources\" /S /Y

rem Optionally, exclude .pdb files:
for %%f in ("%PACKAGE_FOLDER%\*.pdb") do del "%%f"

rem === CREATE ZIP PACKAGE ===
echo Creating zip package %OUTPUT_ZIP%...
7z a -tzip "%OUTPUT_ZIP%" "%PACKAGE_FOLDER%\*" >nul
if errorlevel 1 (
    echo ERROR: 7z compression failed.
    exit /b 1
)

rem === CLEANUP PACKAGE DIRECTORY ===
echo Cleaning up temporary files...
rmdir /s /q "%PACKAGE_FOLDER%"

echo.
echo Build and packaging complete: %OUTPUT_ZIP%
echo.

endlocal
pause
