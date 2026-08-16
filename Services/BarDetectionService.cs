using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TaskTile.Services;

/// <summary>
/// Detects third-party bar/taskbar replacements (YASB, Windhawk, PowerToys Command Palette, etc.)
/// and writes a log to %LOCALAPPDATA%\TaskTile\DetectionLogs\detection.log
/// </summary>
public static class BarDetectionService
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaskTile", "DetectionLogs");

    public record DetectionResult(bool Detected, string BarName, string Detail);

    public static DetectionResult Detect()
    {
        var hits = new List<(string name, string detail)>();

        // ── YASB ──────────────────────────────────────────────────────────────
        TryCheck(hits, "YASB",
            () => Directory.Exists(Path.Combine(
                      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "yasb"))
               || File.Exists(Path.Combine(
                      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                      "Programs", "yasb", "yasb.exe"))
               || IsProcessRunning("yasb"),
            "YASB (Yet Another Status Bar) config directory or process detected.");

        // ── Windhawk ──────────────────────────────────────────────────────────
        TryCheck(hits, "Windhawk",
            () => File.Exists(Path.Combine(
                      Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                      "Windhawk", "Windhawk.exe"))
               || IsProcessRunning("Windhawk"),
            "Windhawk executable or process detected.");

        // ── PowerToys Command Palette ──────────────────────────────────────────
        TryCheck(hits, "PowerToys Command Palette",
            () =>
            {
                var ptPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PowerToys");
                return (Directory.Exists(ptPath) &&
                        Directory.EnumerateFiles(ptPath, "PowerToys.exe", SearchOption.AllDirectories).Any())
                    || IsProcessRunning("PowerToys");
            },
            "PowerToys (with potential Command Palette / FancyZones taskbar integration) detected.");

        // ── StartAllBack / StartIsBack ─────────────────────────────────────────
        TryCheck(hits, "StartAllBack / StartIsBack",
            () => IsProcessRunning("StartAllBack") || IsProcessRunning("StartIsBack64")
               || IsProcessRunning("StartIsBack32"),
            "StartAllBack or StartIsBack taskbar replacement process detected.");

        // ── Rainmeter ─────────────────────────────────────────────────────────
        TryCheck(hits, "Rainmeter",
            () => IsProcessRunning("Rainmeter")
               || File.Exists(Path.Combine(
                      Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                      "Rainmeter", "Rainmeter.exe")),
            "Rainmeter (potential custom taskbar/bar skin) detected.");

        WriteLog(hits);

        if (hits.Count > 0)
        {
            var primary = hits[0];
            return new DetectionResult(true, primary.name, primary.detail);
        }
        return new DetectionResult(false, string.Empty, string.Empty);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void TryCheck(
        List<(string, string)> hits, string name, Func<bool> check, string detail)
    {
        try { if (check()) hits.Add((name, detail)); }
        catch { /* ignore access errors */ }
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch { return false; }
    }

    private static void WriteLog(List<(string name, string detail)> hits)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var logPath = Path.Combine(LogDir, "detection.log");
            var lines = new List<string>
            {
                $"[TaskTile Detection Log — {DateTime.Now:yyyy-MM-dd HH:mm:ss}]",
                $"Machine: {Environment.MachineName}  OS: {Environment.OSVersion}",
                string.Empty
            };

            if (hits.Count == 0)
                lines.Add("No third-party bar modifications detected.");
            else
                foreach (var (name, detail) in hits)
                    lines.Add($"[DETECTED] {name}: {detail}");

            File.WriteAllLines(logPath, lines);
        }
        catch { /* log write failure is non-fatal */ }
    }

    public static string LogPath =>
        Path.Combine(LogDir, "detection.log");
}
