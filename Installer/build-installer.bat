@echo off
echo ============================================
echo  Telegram Smart Organizer v2.1 - Build Installer
echo ============================================
echo.

:: Configuration
set PROJECT_DIR=%~dp0..
set PUBLISH_DIR=%PROJECT_DIR%\publish
set INSTALLER_DIR=%PROJECT_DIR%\Installer
set VERSION=2.1.0

:: Step 1: Clean previous build
echo [1/5] Cleaning previous build...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%INSTALLER_DIR%\Output" rmdir /s /q "%INSTALLER_DIR%\Output"

:: Step 2: Run tests first
echo [2/5] Running tests...
cd /d "%PROJECT_DIR%"
dotnet test --verbosity minimal

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Tests failed! Fix tests before building installer.
    pause
    exit /b 1
)

echo.
echo All tests passed!
echo.

:: Step 3: Publish the application
echo [3/5] Publishing application (v%VERSION%)...
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

:: Step 4: Check if Inno Setup is installed
echo [4/5] Checking Inno Setup...
set "INNO_PATH="
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "INNO_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "INNO_PATH=C:\Program Files\Inno Setup 6\ISCC.exe"
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

:: Step 5: Build installer
echo [5/5] Building installer...
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
echo  BUILD SUCCESSFUL! (v%VERSION%)
echo ============================================
echo.
echo Installer created at:
echo %INSTALLER_DIR%\Output\
echo.
dir /b "%INSTALLER_DIR%\Output"
echo.
echo Next steps:
echo 1. Test the installer on a clean Windows installation
echo 2. Create GitHub release with the installer
echo 3. Update documentation
echo.
pause
