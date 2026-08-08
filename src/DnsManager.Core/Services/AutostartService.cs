using Microsoft.Win32;

using System.Diagnostics;

namespace DnsManager.Core.Services;

public class AutostartService
{
	private const string AppName = "DnsManager";
	private const string TaskName = "DnsManager_Autostart";

	/// <summary>
	/// Проверяет, включен ли автозапуск (существует ли задача в планировщике)
	/// </summary>
	public bool IsEnabled()
	{
		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = "schtasks.exe",
				Arguments = $"/query /tn \"{TaskName}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			using var process = Process.Start(psi);
			process?.WaitForExit();

			// Если код 0, значит задача существует
			return process?.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Включает или отключает автозапуск через Планировщик задач.
	/// </summary>
	public void SetEnabled(bool enable)
	{
		// Очищаем старые нерабочие записи в реестре (если они были созданы старым кодом)
		CleanUpOldRegistryAutostart();

		// Важно использовать ProcessPath, так как Assembly.Location в .NET вернет путь к .dll
		var exePath = Environment.ProcessPath;
		if (string.IsNullOrEmpty(exePath)) return;

		if (enable)
		{
			// /rl highest - запуск с наивысшими правами (без окна UAC)
			// /sc onlogon - запуск при входе пользователя
			// /f - принудительная перезапись, если задача уже есть
			var arguments = $"/create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /rl highest /f";
			RunSchtasks(arguments);
		}
		else
		{
			var arguments = $"/delete /tn \"{TaskName}\" /f";
			RunSchtasks(arguments);
		}
	}

	private void RunSchtasks(string arguments)
	{
		var psi = new ProcessStartInfo
		{
			FileName = "schtasks.exe",
			Arguments = arguments,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		using var process = Process.Start(psi);
		process?.WaitForExit();
	}

	private void CleanUpOldRegistryAutostart()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
			if (key?.GetValue(AppName) != null)
			{
				key.DeleteValue(AppName, throwOnMissingValue: false);
			}
		}
		catch
		{
			// Игнорируем ошибки доступа к реестру
		}
	}
}