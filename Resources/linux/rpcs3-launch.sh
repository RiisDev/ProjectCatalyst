#!/bin/sh
set -eu

WIN_APPIMAGE="$1"
shift

SCRIPT_DIR=$(dirname "$0")
sh "$SCRIPT_DIR/rpcs3-link-data.sh"

if command -v winepath >/dev/null 2>&1; then
	APPIMAGE=$(winepath -u "$WIN_APPIMAGE")
else
	APPIMAGE=$(printf '%s' "$WIN_APPIMAGE" | sed 's#^[A-Za-z]:##; s#\\\\#/#g')
fi

chmod +x "$APPIMAGE" 2>/dev/null || true

exec "$APPIMAGE" "$@"
