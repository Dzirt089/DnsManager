namespace DnsManager.Core.Logging;

/// <summary>Логирование действий приложения (реализация в WPF-слое: панель + файл).</summary>
public interface ILogService
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
    void Debug(string message);
}
