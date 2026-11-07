using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace SubtitleStudio;

public static class ToolDownload
{
    public static async Task<(string Whisper, string FFmpeg)> Install(IProgress<string> progress, CancellationToken token)
    {
        Directory.CreateDirectory(Settings.ToolsFolder);
        var whisper = Path.Combine(Settings.ToolsFolder, "whisper", "Release", "whisper-cli.exe");
        var ffmpeg = Path.Combine(Settings.ToolsFolder, "ffmpeg", "bin", "ffmpeg.exe");
        using var client = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SubtitleStudio/0.1.0");
        if (!File.Exists(whisper))
            await InstallArchive(client, "ggml-org/whisper.cpp", "b5454", "whisper-bin-win-cuda-12.4.0-x64.zip", "whisper", "whisper-cli.exe", progress, token);
        if (!File.Exists(ffmpeg))
            await InstallArchive(client, "BtbN/FFmpeg-Builds", "latest", "ffmpeg-master-latest-win64-lgpl.zip", "ffmpeg", "ffmpeg.exe", progress, token);
        if (!File.Exists(whisper) || !File.Exists(ffmpeg)) throw new IOException("Downloaded tools have an unexpected folder layout.");
        return (whisper, ffmpeg);
    }

    static async Task InstallArchive(HttpClient client, string repo, string tag, string name, string destinationName, string executable, IProgress<string> progress, CancellationToken token)
    {
        var destination = Path.Combine(Settings.ToolsFolder, destinationName);
        if (Directory.Exists(destination)) throw new IOException($"Incomplete tool installation found at {destination}. Rename that folder and retry, or select your own executable.");
        progress.Report("Checking official release: " + repo);
        using var metadata = JsonDocument.Parse(await client.GetStringAsync($"https://api.github.com/repos/{repo}/releases/tags/{tag}", token));
        var asset = metadata.RootElement.GetProperty("assets").EnumerateArray().First(x => x.GetProperty("name").GetString() == name);
        var size = asset.GetProperty("size").GetInt64();
        var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
        if (digest == null || !digest.StartsWith("sha256:")) throw new IOException("The upstream release does not provide a SHA-256 digest. Use manual tool setup instead.");
        var url = asset.GetProperty("browser_download_url").GetString()!;
        if (!url.StartsWith($"https://github.com/{repo}/releases/download/", StringComparison.Ordinal)) throw new IOException("Unexpected release download URL.");
        var staging = Path.Combine(Settings.ToolsFolder, ".install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var archive = Path.Combine(staging, "download.zip");
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var target = File.Create(archive))
            {
                var buffer = new byte[131072]; long total = 0; int previous = -1; int count;
                while ((count = await source.ReadAsync(buffer, token)) != 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, count), token);
                    total += count;
                    var percent = (int)(100 * total / size);
                    if (percent != previous) { progress.Report($"Downloading {destinationName}: {percent}%"); previous = percent; }
                }
                if (total != size) throw new IOException("Tool download size mismatch.");
            }
            progress.Report("Verifying SHA-256: " + destinationName);
            await using (var file = File.OpenRead(archive))
                if (!Convert.ToHexString(await SHA256.HashDataAsync(file, token)).Equals(digest[7..], StringComparison.OrdinalIgnoreCase)) throw new IOException("Tool download SHA-256 mismatch.");
            progress.Report("Extracting " + destinationName + "...");
            var extracted = Path.Combine(staging, "extracted");
            await Task.Run(() => ZipFile.ExtractToDirectory(archive, extracted), token);
            token.ThrowIfCancellationRequested();
            var exe = Directory.EnumerateFiles(extracted, executable, SearchOption.AllDirectories).Single();
            var root = destinationName == "ffmpeg" ? Directory.GetParent(Path.GetDirectoryName(exe)!)!.FullName : extracted;
            var expected = destinationName == "ffmpeg" ? Path.Combine(root, "bin", executable) : Path.Combine(root, "Release", executable);
            if (!File.Exists(expected)) throw new IOException("Unexpected upstream archive layout.");
            Directory.Move(root, destination);
        }
        finally { Directory.Delete(staging, true); }
    }
}
