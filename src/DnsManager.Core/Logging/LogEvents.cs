namespace DnsManager.Core.Logging;

/// <summary>Имена событий для структурного логирования.</summary>
public static class LogEvents
{
	public const string App = "app";
	public const string AppStartup = "app.startup";
	public const string AppExit = "app.exit";
	public const string AdaptersLoad = "network.adapters.load";
	public const string AdaptersResult = "network.adapters.result";
	public const string DnsReadState = "dns.state.read";
	public const string DnsEnable = "dns.enable";
	public const string DnsDisable = "dns.disable";
	public const string DnsApplyPreset = "dns.apply";
	public const string DnsStartupAutoEnable = "dns.startup.autoenable";
	public const string DnsScheduleToggle = "dns.schedule.toggle";
	public const string DnsScheduleTrigger = "dns.schedule.trigger";
	public const string PresetCreate = "preset.create";
	public const string PresetDelete = "preset.delete";
	public const string PresetSetDefault = "preset.setdefault";
	public const string ResolveRun = "resolve.run";
	public const string ResolveResult = "resolve.result";
	public const string BenchmarkRun = "benchmark.run";
	public const string BenchmarkResult = "benchmark.result";
	public const string AutostartToggle = "autostart.toggle";
	public const string LogClear = "log.clear";
	public const string LogExport = "log.export";
}
