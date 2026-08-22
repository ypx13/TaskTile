# Changelog

All notable changes to TaskTile will be documented in this file.

## [v0.6.0] - Unreleased

### Added
- **Better and more native app groups**: A complete architecture overhaul of the background popup engine using IPC Named Pipes and `AppWindow.Hide()`, bringing launch times to a blistering 0ms.
- **Support for custom taskbars**: Seamless compatibility with custom taskbars (YASB, DockFinder, Nexus, etc.).
- **Native System Tray**: Replaced `H.NotifyIcon` with a native Win32 `SystemTrayManager` that uses `Shell_NotifyIcon` and `TrackPopupMenu`, eliminating issues with invisible icons and unresponsive right-clicks.
- **"Start Pop-ups in background" Setting**: Added an option in settings to keep app groups loaded in the background for instant responsiveness (enabled by default).
- **Flawless Borderless Styling**: The WinUI 3 compositor now dynamically removes borders and titlebars without causing black lines from the DWM on Acrylic and Mica materials.
- Everything we made ever since v0.5!

### Fixed
- Fixed jumping animations when popups lose focus and close.
- Fixed jumping animations during the initial popup sequence when opening a group.
- Fixed the annoying black line appearing on top of the popup window when using light mode or specific backdrops.
- Fixed the tray menu options occasionally failing to bring the main window into the foreground.
