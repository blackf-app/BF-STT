#!/usr/bin/env bash
# Full multi-platform release for BF-STT: bumps the version, builds Windows (cross-
# published), macOS, and Android, then tags and publishes a single GitHub Release with
# all three artifacts attached.
#
# Usage:
#   ./scripts/release-all.sh              # bump patch version, build + release all platforms
#
# Requires: dotnet SDK, gh (authenticated), git, zip.
#
# Platform coverage:
#   - macOS   : built here if running on macOS (skipped on Linux/other).
#   - Windows : cross-published from any OS via `dotnet publish -f net8.0-windows -r win-x64`.
#   - Android : built via BF-STT.Android/build-apk.sh. The Android SDK/JDK are auto-detected
#               (existing provisioned SDK, or falls back to an installed Unity Editor's
#               bundled Android SDK) by .agents/scripts/find-android-sdk.sh.
#
# On failure before the git tag/push/release step, the version bump is left uncommitted
# in the working tree (harmless — re-running would bump again). Run
# `git checkout -- BF-STT.csproj BF-STT.Android/BF-STT.Android.csproj` to reset before retrying.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${PROJECT_DIR}"

CSPROJ="BF-STT.csproj"
ANDROID_CSPROJ="BF-STT.Android/BF-STT.Android.csproj"

DOTNET_BIN="${DOTNET_BIN:-}"
if [[ -z "${DOTNET_BIN}" ]]; then
    if command -v dotnet >/dev/null 2>&1; then DOTNET_BIN="dotnet"
    elif [[ -x "${HOME}/.dotnet/dotnet" ]]; then DOTNET_BIN="${HOME}/.dotnet/dotnet"
    else echo "!! dotnet SDK not found. Install .NET 8 or set DOTNET_BIN=/path/to/dotnet." >&2; exit 1
    fi
fi
export DOTNET_ROOT="${DOTNET_ROOT:-${HOME}/.dotnet}"
export PATH="${HOME}/.dotnet:${PATH}"

command -v gh >/dev/null 2>&1 || { echo "!! GitHub CLI (gh) not found." >&2; exit 1; }
command -v zip >/dev/null 2>&1 || { echo "!! zip not found (needed to package the macOS .app)." >&2; exit 1; }
if ! gh auth status >/dev/null 2>&1; then
    echo "!! Not logged into GitHub. Run 'gh auth login' first." >&2; exit 1
fi

if [[ -n "$(git status --porcelain)" ]]; then
    echo "!! Working tree is not clean. Commit or stash pending changes before releasing." >&2
    git status --short
    exit 1
fi

OS_NAME="$(uname -s)"
BUILD_MAC=0
[[ "${OS_NAME}" == "Darwin" ]] && BUILD_MAC=1

# ---------------------------------------------------------------------------
# 1. Version bump (main app). Mirrors scripts/increment_version.ps1.
# ---------------------------------------------------------------------------
CURRENT_VERSION="$(grep -m1 -o '<Version>[^<]*</Version>' "${CSPROJ}" | sed 's/<[^>]*>//g')"
if [[ -z "${CURRENT_VERSION}" ]]; then
    echo "!! Could not find <Version> in ${CSPROJ}" >&2; exit 1
fi

IFS='.' read -r major minor patch <<< "${CURRENT_VERSION}"
patch=$((patch + 1))
if [[ "${patch}" -gt 999 ]]; then patch=0; minor=$((minor + 1)); fi
NEW_VERSION="${major}.${minor}.${patch}"
echo "--> Bumping version: ${CURRENT_VERSION} -> ${NEW_VERSION}"
sed -i.bak -E \
    -e "s#<Version>[0-9]+\.[0-9]+\.[0-9]+</Version>#<Version>${NEW_VERSION}</Version>#" \
    -e "s#<AssemblyVersion>[0-9]+\.[0-9]+\.[0-9]+</AssemblyVersion>#<AssemblyVersion>${NEW_VERSION}</AssemblyVersion>#" \
    -e "s#<FileVersion>[0-9]+\.[0-9]+\.[0-9]+</FileVersion>#<FileVersion>${NEW_VERSION}</FileVersion>#" \
    -e "s#<InformationalVersion>[0-9]+\.[0-9]+\.[0-9]+</InformationalVersion>#<InformationalVersion>${NEW_VERSION}</InformationalVersion>#" \
    "${CSPROJ}"
rm -f "${CSPROJ}.bak"

# Keep the Android app version in lockstep with the main version (versionCode still
# strictly increments so every APK supersedes the previous one).
echo "--> Syncing Android app version to ${NEW_VERSION}"
"${SCRIPT_DIR}/../.agents/scripts/bump-android-version.sh" "${ANDROID_CSPROJ}" "${NEW_VERSION}"

TAG="v${NEW_VERSION}"
if git rev-parse "${TAG}" >/dev/null 2>&1; then
    echo "!! Tag ${TAG} already exists. Aborting before touching the working tree further." >&2
    git checkout -- "${CSPROJ}" "${ANDROID_CSPROJ}"
    exit 1
fi

# ---------------------------------------------------------------------------
# 2. Build all applicable platforms.
# ---------------------------------------------------------------------------
ASSETS_DIR="${PROJECT_DIR}/publish/release-assets"
rm -rf "${ASSETS_DIR}"
mkdir -p "${ASSETS_DIR}"

echo "--> Building Windows (net8.0-windows, win-x64)..."
WIN_OUT="${PROJECT_DIR}/publish/win"
rm -rf "${WIN_OUT}"
"${DOTNET_BIN}" publish "${CSPROJ}" -f net8.0-windows -r win-x64 -c Release \
    --self-contained true -p:PublishSingleFile=false -p:IsAutoPublishing=true \
    -o "${WIN_OUT}" -nologo -clp:NoSummary -v:q
cp "${WIN_OUT}/BF-STT.exe" "${ASSETS_DIR}/BF-STT-Windows-v${NEW_VERSION}.exe"

if [[ "${BUILD_MAC}" == "1" ]]; then
    echo "--> Building macOS (.app)..."
    if [[ "$(uname -m)" == "x86_64" ]]; then MAC_ARCH="x64"; else MAC_ARCH="arm64"; fi
    DOTNET_BIN="${DOTNET_BIN}" APP_VERSION="${NEW_VERSION}" "${SCRIPT_DIR}/build-mac.sh" "${MAC_ARCH}" Release
    ( cd "${PROJECT_DIR}/publish/mac" && zip -qry "${ASSETS_DIR}/BF-STT-macOS-v${NEW_VERSION}.zip" "BF-STT.app" )
else
    echo "--> Skipping macOS build (not running on macOS)."
fi

echo "--> Building Android (.apk)..."
if BF_NO_BUMP=1 "${PROJECT_DIR}/BF-STT.Android/build-apk.sh"; then
    APK_SRC="${PROJECT_DIR}/dist/BF-STT-Android-v${NEW_VERSION}.apk"
    if [[ -f "${APK_SRC}" ]]; then
        cp "${APK_SRC}" "${ASSETS_DIR}/"
    else
        echo "!! Expected Android artifact not found at ${APK_SRC}" >&2
        exit 1
    fi
else
    echo "!! Android build failed. Aborting release before tagging/publishing." >&2
    exit 1
fi

# ---------------------------------------------------------------------------
# 3. Commit the version bump.
# ---------------------------------------------------------------------------
git add "${CSPROJ}" "${ANDROID_CSPROJ}"
git commit -q -m "Bump version to ${NEW_VERSION} for release"

# ---------------------------------------------------------------------------
# 4. Release notes from commit log since the last tag.
# ---------------------------------------------------------------------------
LAST_TAG="$(git describe --tags --abbrev=0 HEAD^ 2>/dev/null || true)"
if [[ -n "${LAST_TAG}" ]]; then
    LOG_RANGE="${LAST_TAG}..HEAD"
else
    LOG_RANGE="-n 10 HEAD"
fi
NOTES_FILE="$(mktemp)"
{
    echo "## Release ${TAG}"
    echo
    for label_pattern in "New Features:^(Add|Implement|Integrate|New|Feat)" \
                         "Bug Fixes:^(Fix|Resolve|Bug|Hotfix)" \
                         "Improvements:^(Improve|Refactor|Update|Clean|Perf|Optimize)"; do
        label="${label_pattern%%:*}"; pattern="${label_pattern#*:}"
        lines="$(git log ${LOG_RANGE} --pretty=format:'%s' | grep -iE "${pattern}" | grep -v '^Merge ' || true)"
        if [[ -n "${lines}" ]]; then
            echo "### ${label}"
            echo "${lines}" | sed 's/^/- /'
            echo
        fi
    done
    others="$(git log ${LOG_RANGE} --pretty=format:'%s' | grep -viE '^(Add|Implement|Integrate|New|Feat|Fix|Resolve|Bug|Hotfix|Improve|Refactor|Update|Clean|Perf|Optimize|Merge )' || true)"
    if [[ -n "${others}" ]]; then
        echo "### Other Changes"
        echo "${others}" | sed 's/^/- /'
        echo
    fi
} > "${NOTES_FILE}"

# ---------------------------------------------------------------------------
# 5. Tag, push, and publish the GitHub Release with all built assets.
# ---------------------------------------------------------------------------
CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
echo "--> Pushing ${CURRENT_BRANCH} and tag ${TAG}..."
git tag "${TAG}"
git push origin "${CURRENT_BRANCH}"
git push origin "${TAG}"

echo "--> Creating GitHub Release ${TAG}..."
gh release create "${TAG}" "${ASSETS_DIR}"/* --title "Release ${TAG}" --notes-file "${NOTES_FILE}"
rm -f "${NOTES_FILE}"

echo ""
echo "==> RELEASE SUCCESSFUL: ${TAG}"
echo "Assets:"
ls -la "${ASSETS_DIR}"
