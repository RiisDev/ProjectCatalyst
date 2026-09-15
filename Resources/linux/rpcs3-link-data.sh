#!/bin/sh
set -eu

SCRIPT_DIR=$(dirname "$0")
LINK_PATH="$SCRIPT_DIR/rpcs3"
CONFIG_DIR="$HOME/.config/rpcs3"

mkdir -p "$HOME/.config"

if [ -L "$LINK_PATH" ]; then
	CURRENT_TARGET=$(readlink -f "$LINK_PATH" 2>/dev/null || true)
	WANTED_TARGET=$(readlink -f "$CONFIG_DIR" 2>/dev/null || echo "$CONFIG_DIR")
	if [ "$CURRENT_TARGET" != "$WANTED_TARGET" ]; then
		rm "$LINK_PATH"
		ln -s "$CONFIG_DIR" "$LINK_PATH"
	fi
elif [ -e "$LINK_PATH" ]; then
	echo "ProjectCatalyst: $LINK_PATH already exists and isn't a symlink, leaving it as-is" >&2
else
	ln -s "$CONFIG_DIR" "$LINK_PATH"
fi
