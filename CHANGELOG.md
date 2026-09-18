# Changelog

All notable changes to TaskTile will be documented in this file.

## [v0.8.0] - 2026-09-19

### Added
- **60Hz Entrance Animation Overhaul**: Eliminated the sluggish 15-step `SetWindowPos` + `DwmFlush` loop in favor of instant OS window placement and hardware-accelerated 140ms WinUI 3 XAML GPU-composited animations (`CubicEase` ease-out), delivering butter-smooth 60fps+ transitions on standard 60Hz displays.
- **Authentic Fluent 2 Secondary Button Styling**: Rebuilt button hover, pressed, and focus visual states using official Windows 11 WinUI 3 ThemeResource specifications (Dark: `#0FFFFFFF` default, `#15FFFFFF` secondary hover, `#14FFFFFF` stroke; Light: `#B3FFFFFF` default, `#80F9F9F9` secondary hover, `#0F000000` stroke).
- **Start Menu Folder Style**: Modernized Dialog-ish mode into a sleek Windows 11 Start Menu Folder presentation featuring a 2-column layout (24px icons + 12px labels), footer with 14px Semibold title on the left and quick-edit pencil icon on the right, and context menu / flyout acrylic backdrop.
- **ypx.mixUI Toggle ("mix on winui3")**: Toggle between stylized aesthetics/animations and strict native WinUI 3 / Fluent UI behavior. Includes an easter-egg fizz bubble animation when toggled ON, popping out tiny blue soda fizz bubbles featuring ypx OC.
- **Group Settings Fullscreen Toggle**: Added a fullscreen button in the bottom-right corner of the group card flyout to expand settings to fit the window, with smooth restore and auto-reset when closed.
- **Configurable Taskbar Offset**: Added support for `TaskbarOffset` (0–60px, default 12px) allowing users to dial in their preferred spacing from the taskbar.

### Fixed
- **Windows 11 Beta & Windhawk Taskbar Overlap**: Replaced static work area math with live physical taskbar rect detection via `FindWindow("Shell_TrayWnd", null)` to eliminate the 12px overlap/gap bug on Windows 11 Beta builds and custom taskbars.
- **Search Selection Persistence**: Fixed an issue where searching for apps in the group creation and editing dialogs would deselect previously checked apps when clearing or modifying the query.
- **Multi-Drive App Launch Working Directory**: Fixed apps failing to find dependency files or throwing "missing files" errors when installed across different drives by properly setting `WorkingDirectory` to the executable's directory.

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
