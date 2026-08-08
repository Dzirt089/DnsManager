namespace DnsManager.Core.Models;

/// <summary>Цветовая тема приложения.</summary>
public enum UiTheme
{
	Light,
	Dark
}

/// <summary>
/// Настройки окна и UI: размер/позиция окна, высоты/ширины ресайз-блоков, тема.
/// Сериализуются в %LOCALAPPDATA%\DnsManager\uisettings.json.
/// </summary>
public sealed class UiSettings
{
	public const double DefaultWindowWidth = 1100;
	public const double DefaultWindowHeight = 760;
	public const double DefaultLogPanelHeight = 240;
	public const double DefaultPresetListWidth = 240;
	public const double DefaultDnsPanelHeight = 190;

	public double WindowWidth { get; set; } = DefaultWindowWidth;
	public double WindowHeight { get; set; } = DefaultWindowHeight;

	/// <summary>Позиция окна; null = центрировать на экране (CenterScreen).</summary>
	public double? WindowLeft { get; set; }

	public double? WindowTop { get; set; }

	public bool IsMaximized { get; set; }

	public UiTheme Theme { get; set; } = UiTheme.Light;

	public double LogPanelHeight { get; set; } = DefaultLogPanelHeight;

	public double PresetListWidth { get; set; } = DefaultPresetListWidth;

	public double DnsPanelHeight { get; set; } = DefaultDnsPanelHeight;
}
