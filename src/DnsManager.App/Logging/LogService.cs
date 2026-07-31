using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using DnsManager.Core.Logging;

namespace DnsManager.App.Logging;

/// <summary>Логирование в панель UI (ObservableCollection) и файл %LOCALAPPDATA%\DnsManager\logs\.</summary>
public sealed class LogService : ILogService, IDisposable
{
    private const int MaxUiEntries = 500;
    private readonly object _sync = new();
    private readonly StreamWriter _writer;

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public string LogFilePath { get; }

    public LogService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DnsManager", "logs");
        Directory.CreateDirectory(dir);
        LogFilePath = Path.Combine(dir, $"app-{DateTime.Now:yyyyMMdd}.log");
        _writer = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };
        Info("Лог начат.");
    }

    public void Info(string message) => Add("INFO", message, null);
    public void Warn(string message) => Add("WARN", message, null);
    public void Debug(string message) => Add("DEBUG", message, null);
    public void Error(string message, Exception? exception = null) => Add("ERROR", message, exception);

    private void Add(string level, string message, Exception? exception)
    {
        var full = exception is null ? message : $"{message} {exception}";
        lock (_sync)
        {
            _writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{level}] {full}");
        }

        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(() => AddUi(level, full));
    }

    private void AddUi(string level, string full)
    {
        Entries.Add(new LogEntry(DateTime.Now.ToString("HH:mm:ss"), level, full));
        while (Entries.Count > MaxUiEntries)
            Entries.RemoveAt(0);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            Info("Лог завершён.");
            _writer.Dispose();
        }
    }
}
