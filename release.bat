@echo off
setlocal enabledelayedexpansion

pushd "%~dp0"
if errorlevel 1 exit /b 1

echo ===================================================
echo  FileMill GitHub Release Automator
echo ===================================================
echo.

set TAG=0.1

:: 1. Check prerequisites
where gh >nul 2>nul
if errorlevel 1 (
    echo [ERROR] GitHub CLI gh is not installed.
    echo Please install it from https://cli.github.com/
    popd
    exit /b 1
)

gh auth status >nul 2>nul
if errorlevel 1 (
    echo [ERROR] GitHub CLI is not authenticated.
    echo Please run 'gh auth login' to authenticate with GitHub.
    popd
    exit /b 1
)

:: 2. Check git status
git diff-index --quiet HEAD --
if errorlevel 1 (
    echo [WARNING] You have uncommitted changes in your working tree.
    set /p "CONFIRM_DIRTY=Do you want to continue despite uncommitted changes? (y/N): "
    if /i not "!CONFIRM_DIRTY!"=="y" (
        echo Release aborted.
        popd
        exit /b 1
    )
    echo.
)

:: 3. Get release version
if "%TAG%"=="" (
    set /p "TAG=Enter release version tag (e.g., v0.1.0): "
)
if "%TAG%"=="" (
    echo [ERROR] Release version tag cannot be empty.
    popd
    exit /b 1
)

:: Confirm release details
for /f "tokens=*" %%i in ('git branch --show-current') do set "BRANCH=%%i"
echo.
echo Release Configuration:
echo   - Tag Version  : %TAG%
echo   - Git Branch   : %BRANCH%
echo   - ZIP Package  : FileMill-%TAG%.zip
echo.
set /p "CONFIRM_RELEASE=Are you sure you want to build and publish this release? (y/N): "
if /i not "!CONFIRM_RELEASE!"=="y" (
    echo Release aborted.
    popd
    exit /b 1
)
echo.

:: 4. Build the project
echo [1/5] Building release binaries...
call build.bat
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed. Release aborted.
    popd
    exit /b 1
)

:: 5. Package release files
echo.
echo [2/5] Packaging release files...
if exist dist rmdir /S /Q dist
if exist "FileMill-%TAG%.zip" del "FileMill-%TAG%.zip"

mkdir "dist\FileMill-%TAG%"
if errorlevel 1 (
    echo [ERROR] Failed to create dist folder.
    goto :fail
)

xcopy /E /I /Y "bin\Release\net10.0-windows" "dist\FileMill-%TAG%" >nul
if errorlevel 1 (
    echo [ERROR] Failed to copy build outputs.
    goto :fail
)

:: Remove debug symbol files (.pdb) to keep release package smaller
if exist "dist\FileMill-%TAG%\*.pdb" (
    del /Q "dist\FileMill-%TAG%\*.pdb" >nul
)

:: Create zip archive using PowerShell
powershell -NoProfile -Command "Compress-Archive -Path 'dist\FileMill-%TAG%' -DestinationPath 'FileMill-%TAG%.zip' -Force"
if errorlevel 1 (
    echo [ERROR] Failed to compress release folder.
    goto :fail
)

rmdir /S /Q dist
echo Packaged successfully: FileMill-%TAG%.zip

:: 6. Create Git Tag
echo.
echo [3/5] Creating Git tag '%TAG%'...
git tag -a "%TAG%" -m "Release %TAG%" 2>nul
if errorlevel 1 (
    echo [WARNING] Git tag '%TAG%' already exists locally or failed to create.
    set /p "CONFIRM_TAG=Proceed using the existing tag? (y/N): "
    if /i not "!CONFIRM_TAG!"=="y" (
        echo Release aborted.
        popd
        exit /b 1
    )
) else (
    echo Git tag '%TAG%' successfully created.
)

:: 7. Push commits and tags
echo.
echo [4/5] Pushing branch '%BRANCH%' and tags to GitHub...
git push origin "%BRANCH%" --tags
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to push to GitHub. Release creation aborted.
    popd
    exit /b 1
)

:: 8. Create GitHub Release using gh CLI
echo.
echo [5/5] Creating GitHub Release and uploading FileMill-%TAG%.zip...
gh release create "%TAG%" "FileMill-%TAG%.zip" --title "Release %TAG%" --notes "Release %TAG%"
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to create GitHub Release.
    goto :fail
)

echo.
echo ===================================================
echo  Release '%TAG%' successfully published to GitHub!
echo ===================================================
popd
exit /b 0

:fail
echo.
echo [ERROR] Release failed.
if exist dist rmdir /S /Q dist
popd
exit /b 1
