using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using TaskTile.Models;

namespace TaskTile.Services;

/// <summary>
/// Global Windows Hotkey service for TaskTile.
/// Listens for registered shortcuts (e.g. "Ctrl+Shift+A") and triggers Radial Menus.
/// </summary>
public class HotkeyService
{
    private static HotkeyService? _instance;
    public static HotkeyService Instance => _instance ??= new HotkeyService();

    private IntPtr _hwnd = IntPtr.Zero;
    private DispatcherQueue? _dispatcherQueue;
    private SubclassProc? _subclassProc;
    private readonly Dictionary<int, AppGroup> _registeredHotkeys = new();
    public IReadOnlyDictionary<int, AppGroup> RegisteredHotkeys => _registeredHotkeys;
    public bool IsHookActive => _hwnd != IntPtr.Zero;
    private int _nextId = 9100;

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    public void Initialize(IntPtr hwnd, DispatcherQueue dispatcherQueue)
    {
        _hwnd = hwnd;
        _dispatcherQueue = dispatcherQueue;

        if (_subclassProc == null && _hwnd != IntPtr.Zero)
        {
            _subclassProc = new SubclassProc(WindowSubclass);
            SetWindowSubclass(_hwnd, _subclassProc, 9001, IntPtr.Zero);
        }

        UpdateAllHotkeys();
    }

    private IntPtr WindowSubclass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_registeredHotkeys.TryGetValue(id, out var group))
            {
                _dispatcherQueue?.TryEnqueue(() =>
                {
                    RadialMenuManager.HandleTrigger(group);
                });
                return IntPtr.Zero;
            }
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void UpdateAllHotkeys()
    {
        if (_hwnd == IntPtr.Zero) return;

        // Unregister existing
        foreach (var id in _registeredHotkeys.Keys)
        {
            UnregisterHotKey(_hwnd, id);
        }
        _registeredHotkeys.Clear();

        // Register radial groups
        foreach (var group in GroupService.Instance.Groups.Where(g => g.GroupType == GroupType.Radial))
        {
            if (string.IsNullOrWhiteSpace(group.Shortcut)) continue;

            if (TryParseShortcut(group.Shortcut, out uint modifiers, out uint vk))
            {
                int id = _nextId++;
                if (RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, vk))
                {
                    _registeredHotkeys[id] = group;
                    System.Diagnostics.Debug.WriteLine($"[HotkeyService] Registered '{group.Shortcut}' (ID {id}) for group '{group.Name}'");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[HotkeyService] Failed to register hotkey '{group.Shortcut}' for '{group.Name}' (Error: {Marshal.GetLastWin32Error()})");
                }
            }
        }
    }

    public static bool TryParseShortcut(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (i < parts.Length - 1)
            {
                // Modifier
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                    modifiers |= MOD_CONTROL;
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    modifiers |= MOD_SHIFT;
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    modifiers |= MOD_ALT;
                else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                    modifiers |= MOD_WIN;
            }
            else
            {
                // Key (last part)
                vk = ParseKey(part);
            }
        }

        return vk != 0;
    }

    private static uint ParseKey(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return 0;

        if (keyName.Length == 1)
        {
            char c = char.ToUpperInvariant(keyName[0]);
            if (c >= 'A' && c <= 'Z') return (uint)c;
            if (c >= '0' && c <= '9') return (uint)c;
        }

        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(keyName.Substring(1), out int fNum) && fNum >= 1 && fNum <= 24)
        {
            return (uint)(0x70 + (fNum - 1)); // VK_F1 to VK_F24
        }

        return keyName.ToUpperInvariant() switch
        {
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "ESC" or "ESCAPE" => 0x1B,
            "ENTER" or "RETURN" => 0x0D,
            "BACKSPACE" or "BACK" => 0x08,
            "DELETE" or "DEL" => 0x2E,
            "INSERT" or "INS" => 0x2D,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" or "PGUP" => 0x21,
            "PAGEDOWN" or "PGDN" => 0x22,
            "UP" => 0x26,
            "DOWN" => 0x28,
            "LEFT" => 0x25,
            "RIGHT" => 0x27,
            _ => 0
        };
    }
}
