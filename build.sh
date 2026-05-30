#!/bin/bash

# Change directory to the script's directory
cd "$(dirname "$0")" || exit 1

# Kill running FileMill.exe instances if they exist
# Redirect stderr and ignore failure in case the process is not running or taskkill is not available
if command -v taskkill.exe &> /dev/null; then
    taskkill.exe /f /im FileMill.exe 2>/dev/null || true
fi

PROJECT="FileMill.csproj"
CONFIGURATION="Release"
TARGET_FRAMEWORK="net10.0-windows"

if command -v dotnet &> /dev/null; then
    DOTNET_CMD="dotnet"
elif command -v dotnet.exe &> /dev/null; then
    DOTNET_CMD="dotnet.exe"
else
    echo ".NET SDK was not found. Install the .NET 10 SDK and try again."
    exit 1
fi

echo "Restoring $PROJECT..."
if ! "$DOTNET_CMD" restore "$PROJECT"; then
    echo ""
    echo "Build failed."
    exit 1
fi

echo "Building $PROJECT ($CONFIGURATION)..."
if ! "$DOTNET_CMD" build "$PROJECT" --configuration "$CONFIGURATION" --no-restore; then
    echo ""
    echo "Build failed."
    exit 1
fi

echo ""
echo "Build succeeded."
echo "Output: $(pwd)/bin/$CONFIGURATION/$TARGET_FRAMEWORK"
exit 0
