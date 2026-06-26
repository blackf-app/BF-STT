#!/usr/bin/env bash
# Build BF-STT for macOS.
#
# Usage:
#   ./scripts/build-mac.sh                # default: arm64 (Apple Silicon)
#   ./scripts/build-mac.sh x64            # Intel
#   ./scripts/build-mac.sh arm64 Release  # Release config
#
# Output: ./publish/mac/BF-STT.app

set -euo pipefail

ARCH="${1:-arm64}"
CONFIG="${2:-Release}"
RID="osx-${ARCH}"
APP_NAME="BF-STT"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
PUBLISH_DIR="${PROJECT_DIR}/publish/mac"
APP_BUNDLE="${PUBLISH_DIR}/${APP_NAME}.app"
CONTENTS_DIR="${APP_BUNDLE}/Contents"
MACOS_DIR="${CONTENTS_DIR}/MacOS"
RESOURCES_DIR="${CONTENTS_DIR}/Resources"
DOTNET_BIN="${DOTNET_BIN:-}"

if [[ -z "${DOTNET_BIN}" ]]; then
    if command -v dotnet >/dev/null 2>&1; then
        DOTNET_BIN="dotnet"
    elif [[ -x "${HOME}/.dotnet/dotnet" ]]; then
        DOTNET_BIN="${HOME}/.dotnet/dotnet"
    else
        echo "dotnet SDK not found. Install .NET 8 or set DOTNET_BIN=/path/to/dotnet." >&2
        exit 1
    fi
fi

echo ">>> Cleaning previous bundle..."
rm -rf "${APP_BUNDLE}"
mkdir -p "${MACOS_DIR}" "${RESOURCES_DIR}"

echo ">>> Publishing dotnet (rid=${RID}, config=${CONFIG})..."
"${DOTNET_BIN}" publish "${PROJECT_DIR}/BF-STT.csproj" \
    -f net8.0 \
    -r "${RID}" \
    -c "${CONFIG}" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:IsAutoPublishing=true \
    -o "${MACOS_DIR}"

echo ">>> Generating Info.plist..."
cat > "${CONTENTS_DIR}/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyLists-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>BF-STT</string>
    <key>CFBundleDisplayName</key>
    <string>BF-STT</string>
    <key>CFBundleIdentifier</key>
    <string>com.bf.stt</string>
    <key>CFBundleVersion</key>
    <string>1.0.267</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.267</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>BF-STT</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon.icns</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSUIElement</key>
    <true/>
    <key>NSMicrophoneUsageDescription</key>
    <string>BF-STT needs the microphone to record voice for speech-to-text transcription.</string>
    <key>NSAppleEventsUsageDescription</key>
    <string>BF-STT uses Accessibility to inject transcribed text into the focused application and to listen for the global hotkey.</string>
</dict>
</plist>
PLIST

if [[ -f "${PROJECT_DIR}/bf_stt_icon_1770838132412.ico" ]]; then
    cp "${PROJECT_DIR}/bf_stt_icon_1770838132412.ico" "${RESOURCES_DIR}/" || true
fi

if [[ -f "${PROJECT_DIR}/Assets/MenuBarIconTemplate.png" ]]; then
    cp "${PROJECT_DIR}/Assets/MenuBarIconTemplate.png" "${RESOURCES_DIR}/" || true
fi

chmod +x "${MACOS_DIR}/${APP_NAME}"

echo ">>> Build complete: ${APP_BUNDLE}"
echo ""
echo "Run with:    open ${APP_BUNDLE}"
echo "or directly: ${MACOS_DIR}/${APP_NAME}"
echo ""
echo "First run will prompt for:"
echo "  - Microphone permission (System Settings > Privacy & Security > Microphone)"
echo "  - Accessibility permission for hotkeys + text injection"
echo "    (System Settings > Privacy & Security > Accessibility)"
