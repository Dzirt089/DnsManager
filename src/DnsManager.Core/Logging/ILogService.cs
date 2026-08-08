namespace DnsManager.Core.Logging;

/// <summary>Структурное логирование: уровень + событие + сообщение + именованные свойства.</summary>
public interface ILogService
{
	/// <summary>Полная структурная запись.</summary>
	void Log(LogLevel level, string eventName, string message,
			 IReadOnlyDictionary<string, object?>? properties = null,
			 Exception? exception = null);

	void Info(string message);
	void Info(string eventName, string message, params (string Key, object? Value)[] properties);

	void Warn(string message);
	void Warn(string eventName, string message, params (string Key, object? Value)[] properties);

	void Error(string message, Exception? exception = null);
	void Error(string eventName, string message, Exception? exception = null,
			   params (string Key, object? Value)[] properties);

	void Debug(string message);
	void Debug(string eventName, string message, params (string Key, object? Value)[] properties);
}
