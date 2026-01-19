@echo off
echo ============================================
echo  Telegram Smart Organizer - Build Installer
echo ============================================
echo.

:: Configuration
set PROJECT_DIR=%~dp0..
set PUBLISH_DIR=%PROJECT_DIR%\publish
set INSTALLER_DIR=%PROJECT_DIR%\Installer

:: Step 1: Clean previous build
echo [1/4] Cleaning previous build...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%INSTALLER_DIR%\Output" rmdir /s /q "%INSTALLER_DIR%\Output"

:: Step 2: Publish the application
echo [2/4] Publishing application...
cd /d "%PROJECT_DIR%"
dotnet publish TelegramOrganizer.UI\TelegramOrganizer.UI.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%PUBLISH_DIR%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: dotnet publish failed!
    pause
    exit /b 1
)

echo.
echo Published files:
dir /b "%PUBLISH_DIR%"
echo.

:: Step 3: Check if Inno Setup is installed
echo [3/4] Checking Inno Setup...
set INNO_PATH=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set INNO_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set INNO_PATH=C:\Program Files\Inno Setup 6\ISCC.exe
)

if "%INNO_PATH%"=="" (
    echo.
    echo WARNING: Inno Setup not found!
    echo Please install Inno Setup 6 from: https://jrsoftware.org/isinfo.php
    echo.
    echo Published files are available at: %PUBLISH_DIR%
    echo You can run the app directly or create installer manually.
    pause
    exit /b 0
)

:: Step 4: Build installer
echo [4/4] Building installer...
cd /d "%INSTALLER_DIR%"
"%INNO_PATH%" TelegramOrganizerSetup.iss

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Installer build failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo  BUILD SUCCESSFUL!
echo ============================================
echo.
echo Installer created at:
echo %INSTALLER_DIR%\Output\
echo.
dir /b "%INSTALLER_DIR%\Output"
echo.
pause
