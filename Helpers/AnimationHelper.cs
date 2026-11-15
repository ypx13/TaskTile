using System;
using System.Runtime.InteropServices;
using TaskTile.Services;

namespace TaskTile.Helpers;

public static class AnimationHelper
{
    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    private const int VREFRESH = 116;

    public static int GetCurrentRefreshRate()
    {
        try
        {
            var hdc = GetDC(IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                int hz = GetDeviceCaps(hdc, VREFRESH);
                ReleaseDC(IntPtr.Zero, hdc);
                if (hz > 0) return hz;
            }
        }
        catch { }
        return 60; // fallback
    }

    /// <summary>
    /// Computes duration scale factor based on refresh rate.
    /// 240Hz is the baseline (1.0). Lower refresh rates have longer frame persistence,
    /// so shorter animation durations make the transitions feel equally swift and responsive.
    /// </summary>
    public static double GetRefreshRateScale(int hz)
    {
        if (hz >= 240) return 1.0;
        if (hz >= 165) return 0.94;
        if (hz >= 120) return 0.86;
        if (hz >= 90)  return 0.78;
        if (hz >= 60)  return 0.68;
        if (hz >= 30)  return 0.55;
        return 0.45;
    }

    public static double GetEffectiveDurationMultiplier()
    {
        var s = SettingsService.Current;
        double speed = (s != null && s.AnimationSpeedMultiplier >= 0.2) ? s.AnimationSpeedMultiplier : 1.0;

        double refreshFactor = 1.0;
        if (s != null && s.AdaptiveRefreshRateAnimation)
        {
            int hz = GetCurrentRefreshRate();
            refreshFactor = GetRefreshRateScale(hz);
        }

        // Higher speed multiplier means shorter duration
        return refreshFactor / speed;
    }

    public static TimeSpan ScaleDuration(TimeSpan baseDuration)
    {
        double multiplier = GetEffectiveDurationMultiplier();
        double ms = baseDuration.TotalMilliseconds * multiplier;
        return TimeSpan.FromMilliseconds(Math.Max(25.0, ms));
    }

    public static double ScaleDurationMs(double baseMs)
    {
        double multiplier = GetEffectiveDurationMultiplier();
        return Math.Max(25.0, baseMs * multiplier);
    }
}
