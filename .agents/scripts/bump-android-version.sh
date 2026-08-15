#!/usr/bin/env bash
#
# Bump the BF-STT Android app version.
#
#   * Normalizes <ApplicationDisplayVersion> to the x.x.x form (e.g. 1.2 -> 1.2.0).
#   * Increments the patch component            (1.2.0 -> 1.2.1).
#   * Increments <ApplicationVersion> (Android versionCode) by 1 — it must strictly
#     increase for every installable build.
#
# Usage:
#   bump-android-version.sh [path/to/BF-STT.Android.csproj]
#
# Called automatically by BF-STT.Android/build-apk.sh on every build (see --no-bump).
#
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="${1:-$HERE/../../BF-STT.Android/BF-STT.Android.csproj}"

if [ ! -f "$PROJ" ]; then
  echo "!! csproj not found: $PROJ" >&2
  exit 1
fi

disp="$(sed -n 's:.*<ApplicationDisplayVersion>\([^<]*\)</ApplicationDisplayVersion>.*:\1:p' "$PROJ" | head -n1)"
code="$(sed -n 's:.*<ApplicationVersion>\([^<]*\)</ApplicationVersion>.*:\1:p' "$PROJ" | head -n1)"

if [ -z "$disp" ]; then echo "!! <ApplicationDisplayVersion> not found in $PROJ" >&2; exit 1; fi
if [ -z "$code" ]; then echo "!! <ApplicationVersion> not found in $PROJ" >&2; exit 1; fi

# Normalize the display version to x.x.x, defaulting any missing component to 0.
IFS='.' read -r major minor patch _rest <<< "$disp"
major="$(printf '%s' "${major:-1}" | tr -cd '0-9')"; major="${major:-1}"
minor="$(printf '%s' "${minor:-0}" | tr -cd '0-9')"; minor="${minor:-0}"
patch="$(printf '%s' "${patch:-0}" | tr -cd '0-9')"; patch="${patch:-0}"

new_patch=$((patch + 1))
new_disp="${major}.${minor}.${new_patch}"
new_code=$((code + 1))

tmp="$(mktemp)"
sed \
  -e "s:<ApplicationDisplayVersion>[^<]*</ApplicationDisplayVersion>:<ApplicationDisplayVersion>${new_disp}</ApplicationDisplayVersion>:" \
  -e "s:<ApplicationVersion>[^<]*</ApplicationVersion>:<ApplicationVersion>${new_code}</ApplicationVersion>:" \
  "$PROJ" > "$tmp"
mv "$tmp" "$PROJ"

echo ">> Android version bumped: ${disp} (code ${code}) -> ${new_disp} (code ${new_code})"
