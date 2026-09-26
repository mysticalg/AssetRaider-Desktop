#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
rid="${1:?Usage: build-unix.sh osx-arm64|osx-x64|linux-x64}"
case "$rid" in osx-arm64|osx-x64|linux-x64) ;; *) echo "Unsupported runtime: $rid" >&2; exit 1;; esac
out="$PWD/output/$rid"
mkdir -p "$out" "$PWD/output/releases"
dotnet publish crossplatform/AssetRaider.CrossPlatform.csproj -c Release -r "$rid" --self-contained true -p:UseAppHost=true -o "$out/publish"
cp LICENSE "$out/publish/"
cp crossplatform/README.md "$out/publish/README.md"
cp -R desktop/licenses "$out/publish/"
cp crossplatform/THIRD_PARTY.md "$out/publish/licenses/"
chmod +x "$out/publish/AssetRaider"
find "$out/publish/.playwright/node" -type f -name node -exec chmod +x {} \;
if [[ "$rid" == osx-* ]]; then
  arch=arm64; [[ "$rid" == osx-x64 ]] && arch=x86_64
  swiftc -swift-version 5 -parse-as-library -O -target "$arch-apple-macos14.0" -framework ScreenCaptureKit -framework AVFoundation crossplatform/native/AudioCapture.swift -o "$out/publish/AssetRaider.Audio"
  app="$out/AssetRaider.app"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  cp -a "$out/publish/." "$app/Contents/MacOS/"
  cp crossplatform/native/Info.plist "$app/Contents/Info.plist"
  # Ad-hoc signatures permit local execution integrity checks; these are not Developer ID signatures or notarization.
  codesign --force --deep --sign - "$app"
  "$app/Contents/MacOS/AssetRaider.Audio" --check
  "$app/Contents/MacOS/AssetRaider" --launch-check "$out/checks"
  ditto -c -k --sequesterRsrc --keepParent "$app" "output/releases/AssetRaider-0.4.0-$rid.zip"
else
  "$out/publish/AssetRaider" --launch-check "$out/checks"
  cp scripts/install-linux.sh "$out/publish/install.sh"
  cp scripts/uninstall-linux.sh "$out/publish/uninstall.sh"
  cp docs/favicon.svg "$out/publish/assetraider.svg"
  chmod +x "$out/publish/install.sh" "$out/publish/uninstall.sh"
  tar -czf "output/releases/AssetRaider-0.4.0-$rid.tar.gz" -C "$out" publish --transform='s,^publish,AssetRaider,'
fi
cat "$out/checks/launch-check.json"
