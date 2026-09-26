#!/usr/bin/env bash
set -euo pipefail
source_dir="$(cd "$(dirname "$0")" && pwd)"
target="$HOME/.local/share/AssetRaiderApp"
if [[ -d "$target" && ! -f "$target/.assetraider-owned" ]]; then
  echo "Install folder exists and is not owned by AssetRaider: $target" >&2; exit 1
fi
if [[ "$source_dir" == "$target" ]]; then echo 'Already installed here.'; exit 0; fi
mkdir -p "$target" "$HOME/.local/share/applications"
cp -a "$source_dir/." "$target/"
printf 'AssetRaider\n' > "$target/.assetraider-owned"
# Desktop Exec quoting follows the desktop entry spec, not shell quoting.
escaped="${target//\\/\\\\}"
escaped="${escaped//\"/\\\"}"
escaped="${escaped//\$/\\\$}"
escaped="${escaped//\`/\\\`}"
cat > "$HOME/.local/share/applications/io.github.mysticalg.assetraider.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=AssetRaider
Comment=Udio and Suno playback to WAV
Exec="$escaped/AssetRaider"
Icon=$target/assetraider.svg
Terminal=false
Categories=AudioVideo;Audio;
EOF
echo "Installed. Open AssetRaider from your applications menu."
echo "To uninstall: $target/uninstall.sh"
