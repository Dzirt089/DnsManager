namespace DnsManager.App.Logging;

/// <summary>Запись лога для панели UI.</summary>
public sealed record LogEntry(string Timestamp, string Level, string Message);
