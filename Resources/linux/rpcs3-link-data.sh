#!/bin/sh
set -eu

WIN_APPIMAGE="$1"

if command -v winepath >/dev/null 2>&1; then
	APPIMAGE=$(winepath -u "$WIN_APPIMAGE")
else
	APPIMAGE=$(printf '%s' "$WIN_APPIMAGE" | sed 's#^[A-Za-z]:##; s#\\\\#/#g')
fi

DATA_DIR=$(dirname "$APPIMAGE")
CONFIG_DIR="$HOME/.config/rpcs3"

mkdir -p "$HOME/.config"
mkdir -p "$DATA_DIR"

if [ -L "$CONFIG_DIR" ]; then
	CURRENT_TARGET=$(readlink -f "$CONFIG_DIR" 2>/dev/null || true)
	WANTED_TARGET=$(readlink -f "$DATA_DIR" 2>/dev/null || echo "$DATA_DIR")
	if [ "$CURRENT_TARGET" != "$WANTED_TARGET" ]; then
		rm "$CONFIG_DIR"
		ln -s "$DATA_DIR" "$CONFIG_DIR"
	fi
elif [ -d "$CONFIG_DIR" ]; then
	if [ -z "$(ls -A "$CONFIG_DIR" 2>/dev/null)" ]; then
		rmdir "$CONFIG_DIR"
		ln -s "$DATA_DIR" "$CONFIG_DIR"
	else
		echo "ProjectCatalyst: $CONFIG_DIR already has data in it, leaving it as-is instead of linking to $DATA_DIR" >&2
	fi
elif [ -e "$CONFIG_DIR" ]; then
	echo "ProjectCatalyst: $CONFIG_DIR exists and isn't a directory, leaving it as-is" >&2
else
	ln -s "$DATA_DIR" "$CONFIG_DIR"
fi
