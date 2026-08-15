#!/usr/bin/env bash
#
# Resolve ANDROID_HOME and JAVA_HOME for building the BF-STT Android APK, without
# requiring a manually-provisioned Android Studio SDK.
#
# Resolution order:
#   1. Already-exported ANDROID_HOME / ANDROID_SDK_ROOT and JAVA_HOME, if valid.
#   2. The Android Studio default SDK location on this machine, if present.
#   3. The Android SDK + JDK bundled inside an installed Unity Editor (Android Build
#      Support module), auto-detected under the Unity Hub install root. The newest
#      installed editor version that has an Android SDK bundle wins.
#
# Usage:
#   eval "$(.agents/scripts/find-android-sdk.sh)"
#   # exports ANDROID_HOME, ANDROID_SDK_ROOT, JAVA_HOME on success
#
# Exits non-zero (with a diagnostic on stderr, nothing on stdout) if no SDK/JDK
# combination could be found.
#
set -euo pipefail

candidate_android_home="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
candidate_java_home="${JAVA_HOME:-}"

sdk_is_valid() {
    [ -n "$1" ] && [ -d "$1/platforms" ] && [ -d "$1/build-tools" ]
}

jdk_is_valid() {
    [ -n "$1" ] && [ -x "$1/bin/java" ]
}

if ! sdk_is_valid "${candidate_android_home}"; then
    candidate_android_home=""
fi
if ! jdk_is_valid "${candidate_java_home}"; then
    candidate_java_home=""
fi

# 2. Common pre-provisioned locations (Android Studio default, or this repo's own
#    documented dev setup for BF-STT).
if [ -z "${candidate_android_home}" ]; then
    for p in "${HOME}/Library/Android/sdk" "${HOME}/Android/Sdk"; do
        if sdk_is_valid "$p"; then candidate_android_home="$p"; break; fi
    done
fi
if [ -z "${candidate_java_home}" ]; then
    for p in "${HOME}"/android-jdk/jdk-*/Contents/Home "${HOME}"/android-jdk/jdk-*; do
        if jdk_is_valid "$p"; then candidate_java_home="$p"; break; fi
    done
fi

# 3. Fall back to a Unity Editor's bundled Android SDK / OpenJDK (Android Build
#    Support module). Pick the newest editor version that actually has one installed.
if [ -z "${candidate_android_home}" ] || [ -z "${candidate_java_home}" ]; then
    unity_editor_roots=()
    for base in "/Applications/Unity/Hub/Editor" "${HOME}/Applications/Unity/Hub/Editor"; do
        [ -d "$base" ] || continue
        while IFS= read -r -d '' d; do unity_editor_roots+=("$d"); done \
            < <(find "$base" -mindepth 1 -maxdepth 1 -type d -print0 2>/dev/null | sort -zr)
    done

    for editor in "${unity_editor_roots[@]}"; do
        pe="${editor}/PlaybackEngines/AndroidPlayer"
        if [ -z "${candidate_android_home}" ] && sdk_is_valid "${pe}/SDK"; then
            candidate_android_home="${pe}/SDK"
        fi
        if [ -z "${candidate_java_home}" ] && jdk_is_valid "${pe}/OpenJDK"; then
            candidate_java_home="${pe}/OpenJDK"
        fi
        [ -n "${candidate_android_home}" ] && [ -n "${candidate_java_home}" ] && break
    done
fi

if [ -z "${candidate_android_home}" ]; then
    echo "!! Could not locate an Android SDK (checked ANDROID_HOME, ~/Library/Android/sdk, ~/Android/Sdk, and installed Unity Editors). Install Android Build Support via Unity Hub, or set ANDROID_HOME manually." >&2
    exit 1
fi
if [ -z "${candidate_java_home}" ]; then
    echo "!! Could not locate a JDK (checked JAVA_HOME, ~/android-jdk, and installed Unity Editors' bundled OpenJDK). Install a JDK 17, or set JAVA_HOME manually." >&2
    exit 1
fi

echo "export ANDROID_HOME=\"${candidate_android_home}\""
echo "export ANDROID_SDK_ROOT=\"${candidate_android_home}\""
echo "export JAVA_HOME=\"${candidate_java_home}\""
