#!/usr/bin/env bash
#
# Build the BF-STT Android APK (signed, self-contained, installable).
#
# Prereqs on this machine (already provisioned):
#   - .NET 8 SDK           : ~/.dotnet  (+ android workload, API 34)
#   - Microsoft OpenJDK 17 : ~/android-jdk/jdk-17.0.19+10
#   - Android SDK          : ~/Library/Android/sdk (platform android-34, build-tools 34.0.0)
#
# Notes:
#   * EmbedAssembliesIntoApk=true (set in the .csproj) makes the APK SELF-CONTAINED, so a
#     directly-installed/sideloaded APK does not crash with "No assemblies found ... Exiting"
#     (that abort means the app was expecting IDE Fast Deployment).
#   * Release is signed with the local keystore below (self-signed dev key). For a Play Store
#     release, swap in your own keystore.
#
set -euo pipefail

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
export JAVA_HOME="$HOME/android-jdk/jdk-17.0.19+10/Contents/Home"
export ANDROID_HOME="$HOME/Library/Android/sdk"
export ANDROID_SDK_ROOT="$ANDROID_HOME"

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="$HERE/BF-STT.Android.csproj"
CONFIG="${1:-Release}"          # Release (default) or Debug
KS="$HERE/bfstt-release.keystore"

# Create the dev keystore on first run.
if [ ! -f "$KS" ]; then
  echo ">> Creating self-signed release keystore"
  "$JAVA_HOME/bin/keytool" -genkeypair -v \
    -keystore "$KS" -alias bfstt -keyalg RSA -keysize 2048 -validity 10000 \
    -storepass bfstt123 -keypass bfstt123 \
    -dname "CN=BF-STT, O=BF Company, L=Hanoi, C=VN"
fi

echo ">> Building $PROJ ($CONFIG)"
if [ "$CONFIG" = "Release" ]; then
  dotnet publish "$PROJ" -c Release -f net8.0-android \
    -p:AndroidSdkDirectory="$ANDROID_HOME" \
    -p:JavaSdkDirectory="$JAVA_HOME" \
    -p:AcceptAndroidSdkLicenses=true \
    -p:AndroidKeyStore=true \
    -p:AndroidSigningKeyStore="$KS" \
    -p:AndroidSigningStorePass=bfstt123 \
    -p:AndroidSigningKeyAlias=bfstt \
    -p:AndroidSigningKeyPass=bfstt123
else
  dotnet build "$PROJ" -c Debug \
    -p:AndroidSdkDirectory="$ANDROID_HOME" \
    -p:JavaSdkDirectory="$JAVA_HOME" \
    -p:AcceptAndroidSdkLicenses=true
fi

echo ">> APK(s):"
find "$HERE/bin/$CONFIG/net8.0-android" -name "*-Signed.apk" -maxdepth 1
