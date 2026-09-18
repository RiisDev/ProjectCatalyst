# ProjectCatalyst

A WPF console-style emulator frontend/launcher. Presents a tile-based platform
selector that hands off into a full-screen UI per emulated system, built to
look and feel like that console's own native menu — not one shared style.

## Status

PlayStation 3 (via RPCS3), styled after the PS3 XMB, is the only fully working
console right now. Other systems (Xbox 360, PS4, Wii) exist as catalog
placeholders and are planned to get their own native-style menu (Xbox 360
dashboard, PS4 menu, Wii Menu, etc) rather than reusing the XMB UI.

See [Resources/README.md](Resources/README.md) for credits on the assets used.

## Known issues / limitations

- **Gamepad support is XInput-only.** Xbox and Xbox-compatible controllers
  work out of the box. DualShock/DualSense and other non-XInput pads won't be
  recognized unless mapped through a compatibility layer (DS4Windows, Steam
  Input, etc).
- **Running under Wine (Linux) is under active testing.** The app targets
  Windows/WPF, but is being validated to also run under Wine. Background
  video/audio playback goes through Windows Media Foundation, which Wine only
  supports via `winegstreamer` — this requires the GStreamer plugin package
  providing WAV/audio decode support to be installed on the host system. Without
  it, background video/audio will silently fail to play under Wine.
- **Unhandled exceptions currently crash the app** with no user-facing
  message. Known gap, planned for later.
- **No custom application icon yet** — uses the default .NET executable icon.
  Planned for later.
