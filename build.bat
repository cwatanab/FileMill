@echo off
setlocal

pushd "%~dp0"
if errorlevel 1 exit /b 1

taskkill /f /im FileMill.exe

set "PROJECT=FileMill.csproj"
set "CONFIGURATION=Release"
set "TARGET_FRAMEWORK=net10.0-windows"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET SDK was not found. Install the .NET 10 SDK and try again.
    popd
    exit /b 1
)

echo Restoring %PROJECT%...
dotnet restore "%PROJECT%"
if errorlevel 1 goto :fail

echo Building %PROJECT% (%CONFIGURATION%)...
dotnet build "%PROJECT%" --configuration "%CONFIGURATION%" --no-restore
if errorlevel 1 goto :fail

echo.
echo Build succeeded.
echo Output: "%CD%\bin\%CONFIGURATION%\%TARGET_FRAMEWORK%"
popd
exit /b 0

:fail
echo.
echo Build failed.
popd
exit /b 1
