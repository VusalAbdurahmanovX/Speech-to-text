using System.Diagnostics;
using System.Text;

namespace SubtitleStudio;

public static class ProcessRunner
{
    public static async Task<string> Run(string executable, IEnumerable<string> args, CancellationToken token, Action<string>? line = null, Action<int>? started = null)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException($"Could not start {executable}");
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var output = new StringBuilder();
        async Task Read(System.IO.StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } text)
            {
                lock (output) output.AppendLine(text);
                line?.Invoke(text);
            }
        }
        var stdout = Read(process.StandardOutput);
        var stderr = Read(process.StandardError);
        started?.Invoke(process.Id);
        await Task.WhenAll(stdout, stderr, process.WaitForExitAsync());
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) throw new InvalidOperationException($"{System.IO.Path.GetFileName(executable)} exited with {process.ExitCode}:\n{output.ToString()[Math.Max(0, output.Length - 6000)..]}");
        return output.ToString();
    }
}
