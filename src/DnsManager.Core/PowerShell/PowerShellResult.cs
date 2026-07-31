namespace DnsManager.Core.PowerShell;

/// <summary>Результат выполнения PowerShell-скрипта.</summary>
public sealed record PowerShellResult(int ExitCode, string StdOut, string StdErr)
{
    public bool IsSuccess => ExitCode == 0;
}
