#!/usr/bin/env bash
set -euo pipefail
target="$HOME/.local/share/AssetRaiderApp"
# Delete only the fixed, marked app directory; never the browser profile or recordings.
if [[ ! -d "$target" || -L "$target" || ! -f "$target/.assetraider-owned" || "$(cat "$target/.assetraider-owned")" != AssetRaider ]]; then
  echo "No installer-owned application folder found." >&2; exit 1
fi
resolved="$(cd "$target" && pwd -P)"
boundary="$(cd "$HOME/.local/share" && pwd -P)/AssetRaiderApp"
[[ "$resolved" == "$boundary" ]] || { echo 'Unexpected installation path.' >&2; exit 1; }
rm -rf -- "$resolved"
rm -f -- "$HOME/.local/share/applications/io.github.mysticalg.assetraider.desktop"
echo 'AssetRaider removed. Browser sign-ins and recordings are preserved.'
