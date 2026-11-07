using System.IO;
using System.Text.Json;

namespace SubtitleStudio;

public sealed class Settings
{
    public static string Root { get; } = FindRoot();
    public static string UserData { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SubtitleStudio");
    public static bool IsSourceCheckout => File.Exists(Path.Combine(Root, "src", "SubtitleStudio", "SubtitleStudio.csproj"));
    public static string ToolsFolder => Path.Combine(IsSourceCheckout ? Root : UserData, "tools");
    public static string ResultsFolder => Path.Combine(IsSourceCheckout ? Root : UserData, "artifacts");
    public static string FilePath => Path.Combine(UserData, "settings.json");
    public string WhisperPath { get; set; } = Path.Combine(ToolsFolder, "whisper", "Release", "whisper-cli.exe");
    public string FFmpegPath { get; set; } = File.Exists(Path.Combine(ToolsFolder, "ffmpeg", "bin", "ffmpeg.exe")) ? Path.Combine(ToolsFolder, "ffmpeg", "bin", "ffmpeg.exe") : "ffmpeg.exe";
    public string ModelFolder { get; set; } = Path.Combine(IsSourceCheckout ? Root : UserData, "models");
    public string Model { get; set; } = "large-v3-turbo";
    public string Language { get; set; } = "tr";
    public bool UseCuda { get; set; } = true;
    public int GpuLayers { get; set; } = 99;
    public int BeamSize { get; set; } = 5;
    public int Threads { get; set; } = Math.Max(1, Math.Min(8, Environment.ProcessorCount / 2));

    static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "src", "SubtitleStudio", "SubtitleStudio.csproj"))) return dir.FullName;
        return AppContext.BaseDirectory;
    }

    public static Settings Load() => File.Exists(FilePath)
        ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new() : new();

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        SubtitleFiles.AtomicWrite(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public string? ModelPath(string model) => Directory.Exists(ModelFolder)
        ? Directory.EnumerateFiles(ModelFolder, $"ggml-{model}*.bin")
            .Where(p => Path.GetFileNameWithoutExtension(p) == $"ggml-{model}" || Path.GetFileNameWithoutExtension(p).StartsWith($"ggml-{model}-q", StringComparison.Ordinal))
            .OrderBy(p => p.Length).FirstOrDefault() : null;
}
