using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TaskTile.Helpers;

/// <summary>
/// FireCube GlowUI design system theme helper.
/// </summary>
public static class GlowUITheme
{
    public static (Color Light1, Color Light2, Color Light3, Color BaseAccent) GetAccentColors()
    {
        Color baseAcc = Colors.DodgerBlue;
        if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var sc) && sc is Color c)
        {
            baseAcc = c;
        }
        else if (Application.Current.Resources.TryGetValue("SystemControlHighlightAccentBrush", out var sb) && sb is SolidColorBrush solid)
        {
            baseAcc = solid.Color;
        }

        Color l1 = Application.Current.Resources.TryGetValue("SystemAccentColorLight1", out var r1) && r1 is Color c1 ? c1 : Lighten(baseAcc, 0.25f);
        Color l2 = Application.Current.Resources.TryGetValue("SystemAccentColorLight2", out var r2) && r2 is Color c2 ? c2 : baseAcc;
        Color l3 = Application.Current.Resources.TryGetValue("SystemAccentColorLight3", out var r3) && r3 is Color c3 ? c3 : Lighten(baseAcc, 0.50f);

        return (l1, l2, l3, baseAcc);
    }

    private static Color Lighten(Color c, float factor)
    {
        byte r = (byte)Math.Clamp(c.R + (255 - c.R) * factor, 0, 255);
        byte g = (byte)Math.Clamp(c.G + (255 - c.G) * factor, 0, 255);
        byte b = (byte)Math.Clamp(c.B + (255 - c.B) * factor, 0, 255);
        return Color.FromArgb(c.A, r, g, b);
    }

    /// <summary>
    /// i forgot what to put here
    /// </summary>
    public static LinearGradientBrush CreateGlowBorderBrush(bool isHover = false)
    {
        var (l1, l2, l3, _) = GetAccentColors();
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1)
        };

        if (isHover)
        {
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, l1.R, l1.G, l1.B), Offset = 0.0 });
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(210, l2.R, l2.G, l2.B), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, l3.R, l3.G, l3.B), Offset = 1.0 });
        }
        else
        {
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(120, l1.R, l1.G, l1.B), Offset = 0.0 });
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(60,  l2.R, l2.G, l2.B), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(120, l3.R, l3.G, l3.B), Offset = 1.0 });
        }

        return brush;
    }

    /// <summary>
    /// FireCube signature horizontal accent gradient pipe / line brush.
    /// </summary>
    public static LinearGradientBrush CreateAccentPipeBrush()
    {
        var (l1, l2, l3, _) = GetAccentColors();
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 0)
        };
        brush.GradientStops.Add(new GradientStop { Color = l1, Offset = 0.0 });
        brush.GradientStops.Add(new GradientStop { Color = l2, Offset = 0.5 });
        brush.GradientStops.Add(new GradientStop { Color = l3, Offset = 1.0 });
        return brush;
    }

    /// <summary>
    /// si, ai si
    /// </summary>
    public static SolidColorBrush CreateTileHoverBackgroundBrush()
    {
        var (_, l2, _, _) = GetAccentColors();
        return new SolidColorBrush(Color.FromArgb(45, l2.R, l2.G, l2.B));
    }

    /// <summary>
    /// ok dark bg i see
    /// </summary>
    public static SolidColorBrush CreateCardBackgroundBrush()
    {
        return new SolidColorBrush(Color.FromArgb(220, 24, 24, 24));
    }
}
