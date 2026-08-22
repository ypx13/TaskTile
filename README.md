<div align="center">

# 🪟 TaskTile

**A tactile, native Windows 11 taskbar group launcher built with C# and WinUI 3.**

[![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D4?style=flat&logo=windows11&logoColor=white)](https://github.com/ypx13/TaskTile)
[![Framework](https://img.shields.io/badge/Framework-.NET%208%20%7C%20WinAppSDK%201.6-512BD4?style=flat&logo=dotnet&logoColor=white)](https://github.com/ypx13/TaskTile)
[![UI](https://img.shields.io/badge/UI-WinUI%203-005FB8?style=flat&logo=fluentui&logoColor=white)](https://github.com/ypx13/TaskTile)
[![License](https://img.shields.io/badge/License-Non--Commercial%20Community-orange.svg?style=flat)](LICENSE)

[📦 Download Latest Release](https://github.com/ypx13/TaskTile/releases) • [✨ Features](#-features) • [🚀 Quick Start](#-quick-start) • [🛠️ Build From Source](#%EF%B8%8F-building-from-source) • [📄 License](#-license)

</div>

---

## ✦ Overview

**TaskTile** allows you to organize your desktop workflow by bundling apps, files, and dynamic folders into clean, native taskbar popups. Designed from the ground up with **WinUI 3** and the **Windows App SDK**, TaskTile looks and feels like an integral part of Windows 11.

---

## ✨ Features

### 🗂️ App & File Groups
- **Application Launchers**: Group your workflow apps (Dev, Media, Gaming, Productivity) into single taskbar icons.
- **File Groups**: Bundle frequently used documents, shortcuts, or executables into a quick-access popup.
- **Dynamic Folders**: Point to any directory—TaskTile automatically syncs and displays its live contents with native icons.

### 🎨 5 Unique Layout Styles
- **Classic Grid**: Win11 Start Menu-inspired grid with smooth multi-page pagination and navigation dots.
- **Compact (Row / Column)**: Minimalist, ultra-slim horizontal bar or vertical dock.
- **Modern**: Floating card grid with customizable columns and adaptive sizing.
- **List**: Clean vertical list supporting marquee animations and horizontal scrolling for long titles.
- **Dialog-ish**: Styled card presentation with footer title bar.

### 🪟 Native Windows 11 Design & Materials
- Native **Mica**, **Mica Alt**, and **Desktop Acrylic** backdrops.
- Hardware-accelerated DWM rounded corners and fluent elevation lighting.
- Full Light and Dark mode support with per-group theme overrides.
- Live taskbar tracking with auto-hide synchronization.

### ⚙️ Deep Customization
- **Launch Positions**: Open from Bottom, Top, Left, Right, or Center screen positions.
- **Focus Mode**: Optional background dimming and blur for distraction-free launching.
- **Icon Styling**: Transparent, Monochrome, Accent One-Tone, or Custom Icons.
- **Custom Borders & Geometry**: Customize border colors, float offsets, corner radii, and animations.

---

## 🚀 Quick Start

1. **Download**: Grab `TaskTile.zip` from the [Releases](https://github.com/ypx13/TaskTile/releases) page.
2. **Extract**: Unzip the folder anywhere on your PC (fully portable, no installation needed).
3. **Launch**: Run `TaskTile.exe` to create your first group.
4. **Pin to Taskbar**: Click **Desktop Shortcut** on any group card and pin the generated shortcut to your Windows Taskbar.

---

## 🛠️ Building from Source

### Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (17.8+) with **.NET Desktop Development** and **Windows App SDK** workloads, or [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- Windows 11 (Build 22621+ recommended).

### Build & Run
```powershell
# Clone the repository
git clone https://github.com/ypx13/TaskTile.git
cd TaskTile

# Build Debug
dotnet build -c Debug

# Publish Standalone Release
dotnet publish -c Release -r win-x64 --self-contained true
```

---

## 📄 License & Community Terms

TaskTile is licensed under the **TaskTile Non-Commercial Community License** (see [LICENSE](LICENSE)).

- ✅ **Free & Open for Personal Use**: Free to run, inspect, study, and use.
- ✅ **Community Forks & Enhancements**: You are welcome to fork the project, add features, and build enhanced community editions (e.g. *TaskTile+*).
- 🏷️ **Attribution Required**: You must prominently credit **ypx13** as the original author and link to the original project. You may not claim original ownership or remove author credits.
- 🚫 **Strictly Non-Commercial**: The software and any forks/derivatives **may NOT be sold, paywalled, or commercialized**. It must remain 100% free for everyone.

<p align="center">
  <sub>Built with 💖 using WinUI 3 & .NET 8 • TaskTile by ypx13</sub>
</p>
