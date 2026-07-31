using System.Collections.Specialized;
using System.Windows;
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
}
