#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
rid="${1:?Usage: build-unix.sh osx-arm64|osx-x64|linux-x64}"
case "$rid" in osx-arm64|osx-x64|linux-x64) ;; *) echo "Unsupported runtime: $rid" >&2; exit 1;; esac
out="$PWD/output/$rid"
mkdir -p "$out" "$PWD/output/releases"
publish_options=()
if [[ "$rid" == osx-* ]]; then publish_options+=(-p:PublishSingleFile=true); fi
dotnet publish crossplatform/AssetRaider.CrossPlatform.csproj -c Release -r "$rid" --self-contained true -p:UseAppHost=true "${publish_options[@]}" -o "$out/publish"
cp LICENSE "$out/publish/"
cp crossplatform/README.md "$out/publish/README.md"
cp -R desktop/licenses "$out/publish/"
cp crossplatform/THIRD_PARTY.md "$out/publish/licenses/"
cp crossplatform/licenses/* "$out/publish/licenses/"
chmod +x "$out/publish/AssetRaider"
find "$out/publish/.playwright/node" -type f -name node -exec chmod +x {} \;
if [[ "$rid" == osx-* ]]; then
  arch=arm64; [[ "$rid" == osx-x64 ]] && arch=x86_64
  swiftc -swift-version 5 -parse-as-library -O -target "$arch-apple-macos14.0" -framework ScreenCaptureKit -framework AVFoundation crossplatform/native/AudioCapture.swift -o "$out/publish/AssetRaider.Audio"
  app="$out/AssetRaider.app"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  cp -a "$out/publish/." "$app/Contents/MacOS/"
  cp crossplatform/native/Info.plist "$app/Contents/Info.plist"
  iconset="$out/AssetRaider.iconset"
  mkdir -p "$iconset"
  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" brand/app-icon.png --out "$iconset/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2))
    sips -z "$double" "$double" brand/app-icon.png --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
  done
  iconutil -c icns "$iconset" -o "$app/Contents/Resources/AssetRaider.icns"
  # Browser-driver assets are resources, not a nested executable bundle.
  mv "$app/Contents/MacOS/.playwright" "$app/Contents/Resources/.playwright"
  for resource in "$app/Contents/MacOS/"*; do
    case "$(basename "$resource")" in AssetRaider|AssetRaider.Audio|*.dylib) ;; *) mv "$resource" "$app/Contents/Resources/" ;; esac
  done
  # Ad-hoc signatures permit local execution integrity checks; these are not Developer ID signatures or notarization.
  while IFS= read -r -d '' binary; do
    [[ "$binary" == "$app/Contents/MacOS/AssetRaider" ]] && continue
    if file -b "$binary" | grep -q 'Mach-O'; then codesign --force --sign - "$binary"; fi
  done < <(find "$app" -type f -print0)
  codesign --force --sign - "$app"
  codesign --verify --deep --strict "$app"
  "$app/Contents/MacOS/AssetRaider.Audio" --check
  "$app/Contents/MacOS/AssetRaider" --launch-check "$out/checks"
  ditto -c -k --sequesterRsrc --keepParent "$app" "output/releases/AssetRaider-0.4.4-$rid.zip"
else
  "$out/publish/AssetRaider" --launch-check "$out/checks"
  cp scripts/install-linux.sh "$out/publish/install.sh"
  cp scripts/uninstall-linux.sh "$out/publish/uninstall.sh"
  cp brand/app-icon.png "$out/publish/assetraider.png"
  chmod +x "$out/publish/install.sh" "$out/publish/uninstall.sh"
  tar -czf "output/releases/AssetRaider-0.4.4-$rid.tar.gz" -C "$out" publish --transform='s,^publish,AssetRaider,'
fi
cat "$out/checks/launch-check.json"
