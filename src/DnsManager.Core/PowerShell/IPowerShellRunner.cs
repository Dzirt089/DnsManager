namespace DnsManager.Core.PowerShell;

/// <summary>Выполнение PowerShell-скрипта (powershell.exe) и захват вывода.</summary>
public interface IPowerShellRunner
{
    Task<PowerShellResult> RunAsync(string script, CancellationToken ct = default);
}
