using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace SubtitleStudio;

public static class ModelDownload
{
    public static string SelectFile(string model, IEnumerable<string> available)
    {
        if (!Transcriber.Models.Contains(model)) throw new ArgumentException("Unknown model.");
        var files = available.ToHashSet(StringComparer.Ordinal);
        return new[] { $"ggml-{model}-q5_0.bin", $"ggml-{model}-q5_1.bin", $"ggml-{model}.bin" }.FirstOrDefault(files.Contains)
            ?? throw new IOException($"No supported official download is available for {model}.");
    }
    public static async Task Download(string model, string folder, IProgress<string> progress, CancellationToken token)
    {
        if (!Transcriber.Models.Contains(model)) throw new ArgumentException("Unknown model.");
        Directory.CreateDirectory(folder);
        using var client = new HttpClient { Timeout = TimeSpan.FromHours(6) };
        using var metadata = JsonDocument.Parse(await client.GetStringAsync("https://huggingface.co/api/models/ggerganov/whisper.cpp/tree/main?limit=1000", token));
        var name = SelectFile(model, metadata.RootElement.EnumerateArray().Select(x => x.GetProperty("path").GetString()!));
        var path = Path.Combine(folder, name);
        if (File.Exists(path)) throw new IOException("This model already exists. Choose another model or model folder.");
        progress.Report("Selected official model file: " + name);
        var entry = metadata.RootElement.EnumerateArray().First(x => x.GetProperty("path").GetString() == name);
        var hash = entry.GetProperty("lfs").GetProperty("oid").GetString()!;
        var size = entry.GetProperty("size").GetInt64();
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using var response = await client.GetAsync("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/" + name, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var target = File.Create(temp))
            {
                var buffer = new byte[131072];
                long total = 0; int previous = -1;
                int count;
                while ((count = await source.ReadAsync(buffer, token)) != 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, count), token);
                    total += count;
                    var percent = (int)(total * 100 / size);
                    if (percent != previous) { progress.Report($"Downloading {model}: {percent}% ({total / 1048576} MiB)"); previous = percent; }
                }
                if (total != size) throw new IOException("Model download size mismatch.");
            }
            progress.Report("Verifying SHA-256...");
            await using (var stream = File.OpenRead(temp))
                if (!Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Model SHA-256 mismatch.");
            File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
