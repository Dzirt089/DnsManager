using DnsManager.Core.Logging;

using System.Text.Json;

namespace DnsManager.Tests;

public class LogEntrySerializerTests
{
	[Fact]
	public void ToJsonLine_ContainsStructuredFields()
	{
		var entry = new LogEntry
		{
			Timestamp = new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero),
			Level = LogLevel.Info,
			Event = "dns.enable",
			Message = "Включение ручного DNS",
			Properties = new Dictionary<string, object?> { ["Adapter"] = "Ethernet", ["ExitCode"] = 0 }
		};

		var line = LogEntrySerializer.ToJsonLine(entry);

		using var doc = JsonDocument.Parse(line);
		var root = doc.RootElement;
		Assert.Equal("2026-07-31T12:00:00+00:00", root.GetProperty("Timestamp").GetString());
		Assert.Equal("Info", root.GetProperty("Level").GetString());
		Assert.Equal("dns.enable", root.GetProperty("Event").GetString());
		Assert.Equal("Включение ручного DNS", root.GetProperty("Message").GetString());
		Assert.Equal("Ethernet", root.GetProperty("Properties").GetProperty("Adapter").GetString());
		Assert.Equal(0, root.GetProperty("Properties").GetProperty("ExitCode").GetInt32());
	}

	[Fact]
	public void ToJsonLine_NullPropertiesAndException_AreOmitted()
	{
		var entry = new LogEntry { Message = "Просто сообщение" };

		var line = LogEntrySerializer.ToJsonLine(entry);

		Assert.DoesNotContain("\"Properties\"", line);
		Assert.DoesNotContain("\"Exception\"", line);
	}

	[Fact]
	public void ToJsonArray_ReturnsIndentedArrayOfEntries()
	{
		var entries = new[]
		{
			new LogEntry { Message = "A", Level = LogLevel.Info },
			new LogEntry { Message = "B", Level = LogLevel.Error }
		};

		var json = LogEntrySerializer.ToJsonArray(entries);

		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		Assert.Equal(JsonValueKind.Array, root.ValueKind);
		Assert.Equal(2, root.GetArrayLength());
		Assert.Equal("Error", root[1].GetProperty("Level").GetString());
	}

	[Fact]
	public void LogEntry_DisplayProperties_FormatForUi()
	{
		var entry = new LogEntry
		{
			Level = LogLevel.Error,
			Message = "Ошибка",
			Properties = new Dictionary<string, object?> { ["Code"] = 42, ["Ok"] = false }
		};

		Assert.Equal("ERROR", entry.LevelText);
		Assert.Contains("Code=42", entry.PropertiesText);
		Assert.Contains("Ok=false", entry.PropertiesText);
		Assert.Equal(8, entry.DisplayTimestamp.Length); // HH:mm:ss
	}
}
