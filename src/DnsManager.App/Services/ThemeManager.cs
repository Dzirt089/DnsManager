using System.Windows;
using DnsManager.Core.Models;

namespace DnsManager.App.Services;

/// <summary>
/// Применение цветовой темы: заменяет словарь кистей (LightTheme.xaml / DarkTheme.xaml)
/// в ресурсах приложения. Контролы используют DynamicResource, поэтому смена мгновенная.
/// </summary>
public static class ThemeManager
{
    public const string ThemeNameKey = "ThemeName";

    public static UiTheme Current { get; private set; } = UiTheme.Light;

    public static void Apply(UiTheme theme)
    {
        Current = theme;
        var dicts = Application.Current.Resources.MergedDictionaries;
        for (var i = 0; i < dicts.Count; i++)
        {
            if (dicts[i].Contains(ThemeNameKey))
            {
                dicts[i] = new ResourceDictionary
                {
                    Source = new Uri($"Themes/{theme}Theme.xaml", UriKind.Relative)
                };
                return;
            }
        }
    }

    public static void Toggle() => Apply(Current == UiTheme.Light ? UiTheme.Dark : UiTheme.Light);
}
