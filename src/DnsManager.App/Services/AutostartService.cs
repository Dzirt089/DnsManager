using Microsoft.Win32;

namespace DnsManager.App.Services;

/// <summary>Автозапуск приложения при входе в Windows (реестр HKCU Run).</summary>
public sealed class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DnsManager";

    private static string CurrentCommand => $"\"{Environment.ProcessPath}\"";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName)?.ToString() == CurrentCommand;
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, CurrentCommand);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
