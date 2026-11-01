using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ThemeMode = NEXUS.Core.Enums.ThemeMode;

namespace NEXUS.Desktop.Services;

public interface IThemeService
{
    ThemeMode CurrentMode { get; }
    bool IsDark { get; }
    event Action<bool>? ThemeChanged;
    void ApplyTheme(ThemeMode mode);
    void ToggleTheme();
}

public class ThemeService : IThemeService
{
    public ThemeMode CurrentMode { get; private set; } = ThemeMode.ObsidianDark;
    public bool IsDark => CurrentMode == ThemeMode.ObsidianDark || (CurrentMode == ThemeMode.SystemAdaptive && IsWindowsInDarkMode());
    public event Action<bool>? ThemeChanged;

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
        if (IsDark)
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

        var app = System.Windows.Application.Current;
        if (app == null) return;

        app.Dispatcher.Invoke(() =>
        {
            try
            {
                var newSource = new Uri(useDark ? "/Themes/Colors.Dark.xaml" : "/Themes/Colors.Light.xaml", UriKind.Relative);
                var newDict = new ResourceDictionary { Source = newSource };

                var merged = app.Resources.MergedDictionaries;
                int foundIndex = -1;
                for (int i = 0; i < merged.Count; i++)
                {
                    if (merged[i].Source != null && merged[i].Source.OriginalString.Contains("Colors", StringComparison.OrdinalIgnoreCase))
                    {
                        foundIndex = i;
                        break;
                    }
                }

                if (foundIndex >= 0)
                {
                    merged.RemoveAt(foundIndex);
                    merged.Insert(foundIndex, newDict);
                }
                else
                {
                    merged.Insert(0, newDict);
                }

                // Also populate root app.Resources so dynamic lookups update immediately
                foreach (var key in newDict.Keys)
                {
                    app.Resources[key] = newDict[key];
                }

                foreach (Window win in app.Windows)
                {
                    win.InvalidateVisual();
                }

                ThemeChanged?.Invoke(IsDark);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Theme apply error: {ex.Message}");
            }
        });
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
}