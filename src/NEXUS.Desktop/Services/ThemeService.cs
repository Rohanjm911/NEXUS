using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using ThemeMode = NEXUS.Core.Enums.ThemeMode;

namespace NEXUS.Desktop.Services;

public interface IThemeService
{
    ThemeMode CurrentMode { get; }
    bool IsDark { get; }
    void ApplyTheme(ThemeMode mode);
    void ToggleTheme();
}

public class ThemeService : IThemeService
{
    public ThemeMode CurrentMode { get; private set; } = ThemeMode.ObsidianDark;
    public bool IsDark => CurrentMode == ThemeMode.ObsidianDark || (CurrentMode == ThemeMode.SystemAdaptive && IsWindowsInDarkMode());

    public ThemeService()
    {
        try
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (CurrentMode == ThemeMode.SystemAdaptive && e.Category == UserPreferenceCategory.General)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => ApplyTheme(ThemeMode.SystemAdaptive));
                }
            };
        }
        catch { }
    }

    public void ToggleTheme()
    {
        if (CurrentMode == ThemeMode.ObsidianDark)
        {
            ApplyTheme(ThemeMode.Light);
        }
        else
        {
            ApplyTheme(ThemeMode.ObsidianDark);
        }
    }

    public void ApplyTheme(ThemeMode mode)
    {
        CurrentMode = mode;
        bool useDark = mode switch
        {
            ThemeMode.ObsidianDark => true,
            ThemeMode.Light => false,
            ThemeMode.SystemAdaptive => IsWindowsInDarkMode(),
            _ => true
        };

        if (useDark)
        {
            ApplyEmeraldDarkTheme();
        }
        else
        {
            ApplyEmeraldLightTheme();
        }
    }

    private static bool IsWindowsInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue)
            {
                return intValue == 0;
            }
        }
        catch { }
        return true;
    }

    private static void ApplyEmeraldDarkTheme()
    {
        UpdateColor("Color.Background.Primary", "#10251D");
        UpdateColor("Color.Background.Secondary", "#18231F");
        UpdateColor("Color.Background.Tertiary", "#24332C");
        UpdateColor("Color.Background.Elevated", "#2D3F37");
        UpdateColor("Color.Background.Sidebar", "#141C18");
        UpdateColor("Color.Background.DropZone", "#132E23");

        UpdateColor("Color.Border.Subtle", "#34443B");
        UpdateColor("Color.Border.Active", "#10B981");
        UpdateColor("Color.Border.Hover", "#6EE7B7");
        UpdateColor("Color.Border.DropZone", "#10B981");

        UpdateColor("Color.Accent.Primary", "#10B981");
        UpdateColor("Color.Accent.Hover", "#6EE7B7");
        UpdateColor("Color.Accent.Pressed", "#059669");
        UpdateColor("Color.Accent.Muted", "#1B382B");
        UpdateColor("Color.Accent.Soft", "#A7F3D0");

        UpdateColor("Color.Text.Primary", "#F8FAFC");
        UpdateColor("Color.Text.Secondary", "#9CA3AF");
        UpdateColor("Color.Text.Tertiary", "#6B7280");
        UpdateColor("Color.Text.Disabled", "#4B5563");
        UpdateColor("Color.Text.OnAccent", "#0A1C14");
        UpdateColor("Color.Text.Highlight", "#A7F3D0");
    }

    private static void ApplyEmeraldLightTheme()
    {
        UpdateColor("Color.Background.Primary", "#F8FAFC");
        UpdateColor("Color.Background.Secondary", "#FFFFFF");
        UpdateColor("Color.Background.Tertiary", "#F1F5F9");
        UpdateColor("Color.Background.Elevated", "#FFFFFF");
        UpdateColor("Color.Background.Sidebar", "#F1F5F4");
        UpdateColor("Color.Background.DropZone", "#EDF9F4");

        UpdateColor("Color.Border.Subtle", "#CBD5E1");
        UpdateColor("Color.Border.Active", "#10B981");
        UpdateColor("Color.Border.Hover", "#059669");
        UpdateColor("Color.Border.DropZone", "#10B981");

        UpdateColor("Color.Accent.Primary", "#10B981");
        UpdateColor("Color.Accent.Hover", "#059669");
        UpdateColor("Color.Accent.Pressed", "#047857");
        UpdateColor("Color.Accent.Muted", "#D1FAE5");
        UpdateColor("Color.Accent.Soft", "#6EE7B7");

        UpdateColor("Color.Text.Primary", "#10251D");
        UpdateColor("Color.Text.Secondary", "#475569");
        UpdateColor("Color.Text.Tertiary", "#64748B");
        UpdateColor("Color.Text.Disabled", "#94A3B8");
        UpdateColor("Color.Text.OnAccent", "#FFFFFF");
        UpdateColor("Color.Text.Highlight", "#065F46");
    }

    private static void UpdateColor(string colorKey, string hex)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        try
        {
            var parsedColor = (Color)ColorConverter.ConvertFromString(hex);
            app.Resources[colorKey] = parsedColor;

            var brushKey = colorKey.Replace("Color.", "Brush.");
            if (app.Resources.Contains(brushKey))
            {
                if (app.Resources[brushKey] is SolidColorBrush brush && !brush.IsFrozen)
                {
                    brush.Color = parsedColor;
                }
                else
                {
                    app.Resources[brushKey] = new SolidColorBrush(parsedColor);
                }
            }
        }
        catch { }
    }
}
