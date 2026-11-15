using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TaskTile.Services;

/// <summary>
/// Diagnostic and debugging helper service for TaskTile DTM (Developer/Test/Maintenance) Mode.
/// </summary>
public static class DebugDiagnosticService
{
    public static (int Major, int Minor, int Patch, string FullVersion) GetVersionInfo()
    {
        var asm = typeof(App).Assembly;
        var ver = asm.GetName().Version ?? new Version(0, 7, 5, 0);
        string infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
                         ?? $"{ver.Major}.{ver.Minor}.{ver.Build}";
        
        // Strip git commit hashes or suffixes from informational version for clean SemVer
        int plusIdx = infoVer.IndexOf('+');
        if (plusIdx > 0) infoVer = infoVer.Substring(0, plusIdx);

        return (ver.Major, ver.Minor, Math.Max(0, ver.Build), $"v{infoVer}");
    }

    public static string RunGitCommand(string args, int timeoutMs = 1500)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            // Traverse upwards to find .git directory if running from bin
            string? repoDir = baseDir;
            for (int i = 0; i < 6 && repoDir != null; i++)
            {
                if (Directory.Exists(Path.Combine(repoDir, ".git"))) break;
                repoDir = Directory.GetParent(repoDir)?.FullName;
            }

            if (repoDir == null || !Directory.Exists(Path.Combine(repoDir, ".git")))
            {
                // Fallback to known development path
                string devPath = @"C:\Users\yassinh\.gemini\TaskTile";
                if (Directory.Exists(Path.Combine(devPath, ".git"))) repoDir = devPath;
            }

            if (repoDir == null) return string.Empty;

            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = repoDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return string.Empty;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(timeoutMs);
            return output.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetGitBranchAndCommit()
    {
        string branch = RunGitCommand("rev-parse --abbrev-ref HEAD");
        string commit = RunGitCommand("rev-parse --short HEAD");

        if (string.IsNullOrEmpty(branch) && string.IsNullOrEmpty(commit))
            return "git: not found / release binary";

        return $"{branch} ({commit})";
    }

    public static string GetChangesSince07()
    {
        // Check commit count since the 0.7 era (commit 82eca82)
        string count = RunGitCommand("rev-list --count 82eca82..HEAD");
        if (int.TryParse(count, out int num))
        {
            return $"{num} commits since v0.7";
        }

        // Try tag fallback
        string tagCount = RunGitCommand("rev-list --count v0.7..HEAD");
        if (int.TryParse(tagCount, out int tNum))
        {
            return $"{tNum} commits since v0.7";
        }

        return "6+ commits since v0.7";
    }

    public static string GetGitHubPushStatus()
    {
        string unpushed = RunGitCommand("rev-list --count origin/main..HEAD");
        if (int.TryParse(unpushed, out int numAhead))
        {
            if (numAhead == 0)
            {
                return "Yes (HEAD is pushed to GitHub)";
            }
            return $"No ({numAhead} local commit(s) unpushed)";
        }

        // Try parsing git status -s -b
        string statusBranch = RunGitCommand("status -s -b");
        if (statusBranch.Contains("[ahead"))
        {
            return "No (Unpushed local commits)";
        }
        if (statusBranch.Contains("main...origin/main") || statusBranch.Contains("up to date"))
        {
            return "Yes (Pushed to GitHub)";
        }

        return "Unknown (Remote sync check timed out)";
    }

    public static string GetWorkingTreeStatus()
    {
        string porcelain = RunGitCommand("status --porcelain");
        if (string.IsNullOrWhiteSpace(porcelain)) return "Clean (No uncommitted changes)";

        var lines = porcelain.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int modified = lines.Count(l => !l.StartsWith("??"));
        int untracked = lines.Count(l => l.StartsWith("??"));

        return $"{modified} modified, {untracked} untracked files";
    }

    public static List<(string Shortcut, string GroupName)> GetHotkeyStatus()
    {
        var list = new List<(string, string)>();
        try
        {
            foreach (var kvp in HotkeyService.Instance.RegisteredHotkeys)
            {
                list.Add((kvp.Value.Shortcut ?? "Hotkey", kvp.Value.Name));
            }
        }
        catch { }
        return list;
    }

    public static string UserDataRoamingPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTile");

    public static string UserDataLocalPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskTile");

    public static void OpenUserDataFolder(bool local = false)
    {
        string target = local ? UserDataLocalPath : UserDataRoamingPath;
        if (!Directory.Exists(target))
        {
            Directory.CreateDirectory(target);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DebugDiagnosticService] OpenUserDataFolder error: {ex.Message}");
        }
    }

    public static string ExportLogs()
    {
        var sb = new StringBuilder();
        var (major, minor, patch, fullVersion) = GetVersionInfo();

        sb.AppendLine("=================================================");
        sb.AppendLine("           TASKTILE DTM DIAGNOSTIC LOG          ");
        sb.AppendLine("=================================================");
        sb.AppendLine($"Export Date:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Exact Version:     {fullVersion} (Major: {major}, Minor: {minor}, Patch: {patch})");
        sb.AppendLine($"Git Branch/Commit: {GetGitBranchAndCommit()}");
        sb.AppendLine($"Changes Since 0.7: {GetChangesSince07()}");
        sb.AppendLine($"Pushed to GitHub:  {GetGitHubPushStatus()}");
        sb.AppendLine($"Working Tree:      {GetWorkingTreeStatus()}");
        sb.AppendLine($"OS Version:        {Environment.OSVersion}");
        sb.AppendLine($"Machine / User:    {Environment.MachineName} / {Environment.UserName}");
        sb.AppendLine($".NET Runtime:      {Environment.Version}");
        sb.AppendLine($"Process Uptime:    {TimeSpan.FromMilliseconds(Environment.TickCount64):hh\\:mm\\:ss}");
        sb.AppendLine($"Process Memory:    {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");
        sb.AppendLine($"Hotkey Hook:       {(HotkeyService.Instance.IsHookActive ? "Active" : "Inactive")}");
        
        var hotkeys = GetHotkeyStatus();
        sb.AppendLine($"Registered Hotkeys ({hotkeys.Count}):");
        if (hotkeys.Count == 0)
        {
            sb.AppendLine("  (None registered)");
        }
        else
        {
            foreach (var h in hotkeys)
            {
                sb.AppendLine($"  - [{h.Shortcut}] -> {h.GroupName}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("-------------------------------------------------");
        sb.AppendLine("          TASKTILE SETTINGS & APP STATE          ");
        sb.AppendLine("-------------------------------------------------");
        var s = SettingsService.Current;
        sb.AppendLine($"Backdrop Style:       {s.BackdropStyle}");
        sb.AppendLine($"Theme:                {s.Theme}");
        sb.AppendLine($"Launch Side:          {s.LaunchSide}");
        sb.AppendLine($"YpxMixUI:             {s.YpxMixUI}");
        sb.AppendLine($"PersistentDebugMode:  {s.PersistentDebugMode}");
        sb.AppendLine($"FakeAppsRunning:      {AppRunningService.FakeAppsRunningEnabled}");
        sb.AppendLine($"Total App Groups:     {GroupService.Instance.Groups.Count}");

        sb.AppendLine();
        sb.AppendLine("-------------------------------------------------");
        sb.AppendLine("              DETECTION LOG CONTENT              ");
        sb.AppendLine("-------------------------------------------------");
        try
        {
            string detLog = BarDetectionService.LogPath;
            if (File.Exists(detLog))
            {
                sb.AppendLine($"Log File: {detLog}");
                sb.AppendLine(File.ReadAllText(detLog));
            }
            else
            {
                sb.AppendLine($"Log file not found at {detLog}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Error reading detection log: {ex.Message}");
        }

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string exportFile = Path.Combine(desktop, $"TaskTile_Debug_Export_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        File.WriteAllText(exportFile, sb.ToString());

        // Open in explorer selected
        try
        {
            Process.Start("explorer.exe", $"/select,\"{exportFile}\"");
        }
        catch { }

        return exportFile;
    }

    public static (bool Success, string Message) DeleteLogs()
    {
        try
        {
            int deletedCount = 0;
            string detDir = Path.Combine(UserDataLocalPath, "DetectionLogs");
            if (Directory.Exists(detDir))
            {
                var files = Directory.GetFiles(detDir, "*.*", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    try
                    {
                        File.Delete(f);
                        deletedCount++;
                    }
                    catch { }
                }
            }

            // Also check for any loose .log files in UserDataRoaming and UserDataLocal
            foreach (var root in new[] { UserDataLocalPath, UserDataRoamingPath })
            {
                if (Directory.Exists(root))
                {
                    var logs = Directory.GetFiles(root, "*.log", SearchOption.TopDirectoryOnly);
                    foreach (var l in logs)
                    {
                        try { File.Delete(l); deletedCount++; } catch { }
                    }
                }
            }

            return (true, $"Deleted {deletedCount} log file(s). All logs cleared.");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete logs: {ex.Message}");
        }
    }
}
