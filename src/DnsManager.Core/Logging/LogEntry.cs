namespace DnsManager.Core.Logging;

/// <summary>Структурная запись лога: уровень, событие, сообщение и именованные свойства.</summary>
public sealed record LogEntry
{
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

	public LogLevel Level { get; init; } = LogLevel.Info;

	/// <summary>Имя события (см. LogEvents), напр. "dns.enable".</summary>
	public string Event { get; init; } = "app";

	public string Message { get; init; } = "";

	/// <summary>Структурные свойства: ключ = значение (адаптер, пресет, exit code, длительность...).</summary>
	public IReadOnlyDictionary<string, object?>? Properties { get; init; }

	public string? Exception { get; init; }

	// --- Свойства для отображения в UI ---

	public string DisplayTimestamp => Timestamp.ToString("HH:mm:ss");

	public string LevelText => Level switch
	{
		LogLevel.Debug => "DEBUG",
		LogLevel.Warn => "WARN",
		LogLevel.Error => "ERROR",
		_ => "INFO"
	};

	public string PropertiesText =>
		Properties is { Count: > 0 }
			? string.Join(" ", Properties.Select(kv => $"{kv.Key}={FormatValue(kv.Value)}"))
			: "";

	private static string FormatValue(object? value) =>
		value switch
		{
			null => "null",
			string s => s,
			bool b => b ? "true" : "false",
			_ => value.ToString() ?? "null"
		};
}
