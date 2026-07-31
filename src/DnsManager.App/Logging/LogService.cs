using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using DnsManager.Core.Logging;

namespace DnsManager.App.Logging;

/// <summary>
/// Структурное логирование: панель UI (ObservableCollection) + файл JSON Lines
/// (%LOCALAPPDATA%\DnsManager\logs\app-yyyyMMdd.jsonl).
/// </summary>
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
        LogFilePath = Path.Combine(dir, $"app-{DateTime.Now:yyyyMMdd}.jsonl");
        _writer = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };
        Info(LogEvents.AppStartup, "Лог начат.");
    }

    public void Log(LogLevel level, string eventName, string message,
                    IReadOnlyDictionary<string, object?>? properties = null,
                    Exception? exception = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Event = string.IsNullOrEmpty(eventName) ? LogEvents.App : eventName,
            Message = message,
            Properties = properties is { Count: > 0 } ? properties : null,
            Exception = exception?.ToString()
        };

        lock (_sync)
        {
            _writer.WriteLine(LogEntrySerializer.ToJsonLine(entry));
        }

        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(() => AddUi(entry));
    }

    public void Info(string message) => Log(LogLevel.Info, LogEvents.App, message);
    public void Info(string eventName, string message, params (string Key, object? Value)[] properties) =>
        Log(LogLevel.Info, eventName, message, ToDict(properties));

    public void Warn(string message) => Log(LogLevel.Warn, LogEvents.App, message);
    public void Warn(string eventName, string message, params (string Key, object? Value)[] properties) =>
        Log(LogLevel.Warn, eventName, message, ToDict(properties));

    public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, LogEvents.App, message, exception: exception);
    public void Error(string eventName, string message, Exception? exception = null, params (string Key, object? Value)[] properties) =>
        Log(LogLevel.Error, eventName, message, ToDict(properties), exception);

    public void Debug(string message) => Log(LogLevel.Debug, LogEvents.App, message);
    public void Debug(string eventName, string message, params (string Key, object? Value)[] properties) =>
        Log(LogLevel.Debug, eventName, message, ToDict(properties));

    private void AddUi(LogEntry entry)
    {
        Entries.Add(entry);
        while (Entries.Count > MaxUiEntries)
            Entries.RemoveAt(0);
    }

    private static IReadOnlyDictionary<string, object?>? ToDict((string Key, object? Value)[] properties) =>
        properties.Length == 0 ? null : properties.ToDictionary(p => p.Key, p => p.Value);

    public void Dispose()
    {
        lock (_sync)
        {
            Info(LogEvents.AppExit, "Лог завершён.");
            _writer.Dispose();
        }
    }
}
