using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DnsManager.App.Services;
using DnsManager.App.ViewModels;
using DnsManager.Core.Models;
using DnsManager.Core.Services;

namespace DnsManager.App;

public partial class MainWindow : Window
{
    // Минимумы из XAML (MinWidth/MinHeight окна и строк/колонок панелей).
    private const double MinWindowWidth = 980;
    private const double MinWindowHeight = 620;
    private const double MinDnsPanelHeight = 120;
    private const double MinLogPanelHeight = 140;
    private const double MinPresetListWidth = 180;

    // Минимальная видимая часть окна, чтобы применить сохранённую позицию (иначе — CenterScreen).
    private const double MinVisibleWidth = 100;
    private const double MinVisibleHeight = 60;

    private readonly UiSettings _settings;
    private readonly UiSettingsStore _store;
    private readonly DispatcherTimer _saveTimer;

    public MainWindow(UiSettings settings, UiSettingsStore store)
    {
        InitializeComponent();
        _settings = settings;
        _store = store;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveSettings();
        };

        ApplySavedWindowState();
        UpdateThemeButton();

        // Сохранение при любом изменении геометрии окна (с дебаунсом).
        SizeChanged += (_, _) => ScheduleSave();
        LocationChanged += (_, _) => ScheduleSave();
        StateChanged += (_, _) => ScheduleSave();
        Loaded += OnLoaded;
    }

    /// <summary>Применить сохранённые размеры/позицию окна и высоты панелей (до показа окна).</summary>
    private void ApplySavedWindowState()
    {
        Width = Math.Max(MinWindowWidth, _settings.WindowWidth);
        Height = Math.Max(MinWindowHeight, _settings.WindowHeight);

        DnsPanelRow.Height = new GridLength(Math.Max(MinDnsPanelHeight, _settings.DnsPanelHeight));
        LogPanelRow.Height = new GridLength(Math.Max(MinLogPanelHeight, _settings.LogPanelHeight));

        // Позицию применяем только если заметная часть окна остаётся на видимом экране
        // (защита от отключённого/изменённого монитора).
        if (_settings.WindowLeft is double left && _settings.WindowTop is double top)
        {
            var rect = new Rect(left, top, Width, Height);
            var virtualScreen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
            rect.Intersect(virtualScreen);
            if (rect.Width >= MinVisibleWidth && rect.Height >= MinVisibleHeight)
            {
                Left = left;
                Top = top;
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Ширину списка профилей применяем здесь: контент вкладки «Пресеты» создаётся
        // только после первого рендера (TabControl лениво строит неактивные вкладки).
        if (PresetListColumn is not null)
            PresetListColumn.Width = new GridLength(Math.Max(MinPresetListWidth, _settings.PresetListWidth));

        if (_settings.IsMaximized)
            WindowState = WindowState.Maximized;

        if (DataContext is MainViewModel vm)
        {
            ((INotifyCollectionChanged)vm.Logs).CollectionChanged += (_, _) =>
            {
                if (LogList.Items.Count > 0)
                    LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
            };
        }
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Сохранить текущее состояние окна и панелей в uisettings.json.</summary>
    public void SaveSettings()
    {
        var bounds = RestoreBounds;
        _settings.WindowWidth = bounds.Width;
        _settings.WindowHeight = bounds.Height;
        _settings.WindowLeft = bounds.Left;
        _settings.WindowTop = bounds.Top;
        _settings.IsMaximized = WindowState == WindowState.Maximized;

        _settings.DnsPanelHeight = DnsPanelRow.Height.Value;
        _settings.LogPanelHeight = LogPanelRow.Height.Value;
        if (PresetListColumn is not null)
            _settings.PresetListWidth = PresetListColumn.Width.Value;

        _settings.Theme = ThemeManager.Current;
        _store.Save(_settings);
    }

    /// <summary>Переключение цветовой темы; текст кнопки показывает, во что переключит.</summary>
    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        UpdateThemeButton();
        SaveSettings();
    }

    private void UpdateThemeButton()
    {
        ThemeButton.Content = ThemeManager.Current == UiTheme.Light ? "Тёмная тема" : "Светлая тема";
    }

    /// <summary>Сдвиг GridSplitter'а — сохраняем сразу (DragCompleted срабатывает однократно).</summary>
    private void Splitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        SaveSettings();
    }

    /// <summary>Двойной клик по пресету — переименование (фокус в поле имени + выделение текста).</summary>
    private void PresetsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        PresetNameBox.Focus();
        PresetNameBox.SelectAll();
    }

    /// <summary>Выделить весь текст имени при получении фокуса (удобно для быстрого переименования).</summary>
    private void PresetNameBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        PresetNameBox.SelectAll();
    }

    /// <summary>Автосохранение после редактирования ячеек серверов в DataGrid.</summary>
    private void PresetsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit && DataContext is MainViewModel vm)
            vm.Presets.Save();
    }
}
