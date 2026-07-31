using System.Diagnostics;
using System.Text;

namespace DnsManager.Core.PowerShell;

/// <summary>Запускает powershell.exe и возвращает exit code + stdout/stderr.</summary>
public sealed class ProcessPowerShellRunner : IPowerShellRunner
{
    private static readonly string PowerShellPath =
        Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    public async Task<PowerShellResult> RunAsync(string script, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = PowerShellPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException("Не удалось запустить powershell.exe.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return new PowerShellResult(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
