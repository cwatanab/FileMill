#!/bin/bash

# Change directory to the script's directory
cd "$(dirname "$0")" || exit 1

echo "==================================================="
echo " FileMill GitHub Release Automator (Bash Version)"
echo "==================================================="
echo ""

TAG="0.1"

# 1. Check prerequisites
if command -v gh &> /dev/null; then
    GH_CMD="gh"
elif command -v gh.exe &> /dev/null; then
    GH_CMD="gh.exe"
else
    echo "[ERROR] GitHub CLI gh is not installed."
    echo "Please install it from https://cli.github.com/"
    exit 1
fi

if ! "$GH_CMD" auth status &> /dev/null; then
    echo "[ERROR] GitHub CLI is not authenticated."
    echo "Please run 'gh auth login' to authenticate with GitHub."
    exit 1
fi

# 2. Check git status
if ! git diff-index --quiet HEAD --; then
    echo "[WARNING] You have uncommitted changes in your working tree."
    read -r -p "Do you want to continue despite uncommitted changes? (y/N): " CONFIRM_DIRTY
    if [[ ! "$CONFIRM_DIRTY" =~ ^[Yy]$ ]]; then
        echo "Release aborted."
        exit 1
    fi
    echo ""
fi

# 3. Get release version
if [ -z "$TAG" ]; then
    read -r -p "Enter release version tag (e.g., v0.1.0): " TAG
fi
if [ -z "$TAG" ]; then
    echo "[ERROR] Release version tag cannot be empty."
    exit 1
fi

# Confirm release details
BRANCH=$(git branch --show-current)
echo ""
echo "Release Configuration:"
echo "  - Tag Version  : $TAG"
echo "  - Git Branch   : $BRANCH"
echo "  - ZIP Package  : FileMill-$TAG.zip"
echo ""
read -r -p "Are you sure you want to build and publish this release? (y/N): " CONFIRM_RELEASE
if [[ ! "$CONFIRM_RELEASE" =~ ^[Yy]$ ]]; then
    echo "Release aborted."
    exit 1
fi
echo ""

# 4. Build the project
echo "[1/5] Building release binaries..."
chmod +x ./build.sh 2>/dev/null || true
if ! ./build.sh; then
    echo ""
    echo "[ERROR] Build failed. Release aborted."
    exit 1
fi

# 5. Package release files
echo ""
echo "[2/5] Packaging release files..."
rm -rf dist
rm -f "FileMill-${TAG}.zip"

if ! mkdir -p "dist/FileMill-${TAG}"; then
    echo "[ERROR] Failed to create dist folder."
    exit 1
fi

if ! cp -r bin/Release/net10.0-windows/* "dist/FileMill-${TAG}/"; then
    echo "[ERROR] Failed to copy build outputs."
    rm -rf dist
    exit 1
fi

# Remove debug symbol files (.pdb) to keep release package smaller
rm -f dist/FileMill-${TAG}/*.pdb 2>/dev/null || true

# Create zip archive
if command -v powershell.exe &> /dev/null; then
    powershell.exe -NoProfile -Command "Compress-Archive -Path 'dist/FileMill-${TAG}' -DestinationPath 'FileMill-${TAG}.zip' -Force"
elif command -v powershell &> /dev/null; then
    powershell -NoProfile -Command "Compress-Archive -Path 'dist/FileMill-${TAG}' -DestinationPath 'FileMill-${TAG}.zip' -Force"
elif command -v zip &> /dev/null; then
    (cd dist && zip -r "../FileMill-${TAG}.zip" "FileMill-${TAG}")
else
    echo "[ERROR] Neither powershell nor zip command was found to compress the release folder."
    rm -rf dist
    exit 1
fi

if [ ! -f "FileMill-${TAG}.zip" ]; then
    echo "[ERROR] Failed to create ZIP archive."
    rm -rf dist
    exit 1
fi

rm -rf dist
echo "Packaged successfully: FileMill-${TAG}.zip"

# 6. Create Git Tag
echo ""
echo "[3/5] Creating Git tag '$TAG'..."
if ! git tag -a "$TAG" -m "Release $TAG" 2>/dev/null; then
    echo "[WARNING] Git tag '$TAG' already exists locally or failed to create."
    read -r -p "Proceed using the existing tag? (y/N): " CONFIRM_TAG
    if [[ ! "$CONFIRM_TAG" =~ ^[Yy]$ ]]; then
        echo "Release aborted."
        exit 1
    fi
else
    echo "Git tag '$TAG' successfully created."
fi

# 7. Push commits and tags
echo ""
echo "[4/5] Pushing branch '$BRANCH' and tags to GitHub..."
if ! git push origin "$BRANCH" --tags; then
    echo ""
    echo "[ERROR] Failed to push to GitHub. Release creation aborted."
    exit 1
fi

# 8. Create GitHub Release using gh CLI
echo ""
echo "[5/5] Creating GitHub Release and uploading FileMill-$TAG.zip..."
if ! "$GH_CMD" release create "$TAG" "FileMill-$TAG.zip" --title "Release $TAG" --notes "Release $TAG"; then
    echo ""
    echo "[ERROR] Failed to create GitHub Release."
    exit 1
fi

echo ""
echo "==================================================="
echo " Release '$TAG' successfully published to GitHub!"
echo "==================================================="
exit 0
