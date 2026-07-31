using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DnsManager.App.ViewModels;

namespace DnsManager.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            ((INotifyCollectionChanged)vm.Logs).CollectionChanged += (_, _) =>
            {
                if (LogList.Items.Count > 0)
                    LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
            };
        }
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
