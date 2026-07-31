using System.Text.Json;
using System.Text.Json.Serialization;

namespace DnsManager.Core.Logging;

/// <summary>Сериализация структурных записей лога в JSON (JSON Lines / массив).</summary>
public static class LogEntrySerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>Одна запись в одну строку JSON (формат JSON Lines).</summary>
    public static string ToJsonLine(LogEntry entry) =>
        JsonSerializer.Serialize(entry, JsonOptions);

    /// <summary>Красивый JSON-массив всех записей (для экспорта).</summary>
    public static string ToJsonArray(IEnumerable<LogEntry> entries)
    {
        var options = new JsonSerializerOptions(JsonOptions) { WriteIndented = true };
        return JsonSerializer.Serialize(entries.ToList(), options);
    }
}
