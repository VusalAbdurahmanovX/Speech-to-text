using Microsoft.Win32;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;

namespace SubtitleStudio;

public partial class MainWindow : Window
{
    Settings settings;
    readonly ConcurrentQueue<string> messages = new();
    readonly Stopwatch watch = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    CancellationTokenSource? cancellation;
    bool dirty;
    bool refreshingModels;
    string? txtPath;

    public MainWindow()
    {
        InitializeComponent();
        try { settings = Settings.Load(); }
        catch (Exception ex) { settings = new(); MessageBox.Show("Settings could not be read. Defaults loaded.\n" + ex.Message); }
        timer.Tick += (_, _) => UpdateProgress();
        timer.Start();
        dirty = false;
        StatusLabel.Text = $"Ready - {settings.Language} - {(settings.UseCuda ? "CUDA" : "CPU")}";
        RefreshModels();
    }
    void RefreshModels()
    {
        refreshingModels = true;
        try
        {
            var models = Transcriber.Models.Select(name =>
            {
                var path = settings.ModelPath(name);
                return new
                {
                    Name = name,
                    Description = name switch
                    {
                        "tiny" => "Lightweight / quick drafts",
                        "medium" => "Balanced accuracy",
                        "large-v1" => "Original large model",
                        "large-v2" => "Second-generation large",
                        "large-v3" => "Highest accuracy",
                        _ => "Fast large-model transcription"
                    },
                    Installed = path != null,
                    Status = path == null ? "Not installed" : $"Installed - {new FileInfo(path).Length / 1048576} MiB"
                };
            }).ToArray();
            ModelList.ItemsSource = models;
            ModelList.SelectedValue = settings.Model;
            ModelCountLabel.Text = $"{models.Count(m => m.Installed)} of {models.Length} installed";
            BackendLabel.Text = $"{(settings.UseCuda ? "CUDA mode" : "CPU mode")} / {settings.Language}";
            UpdateSelectedModel();
        }
        catch (Exception ex) { Error(ex); }
        finally { refreshingModels = false; }
    }
    void UpdateSelectedModel()
    {
        var path = settings.ModelPath(settings.Model);
        ModelDetails.Text = path == null ? $"Select Download to install {settings.Model}." : Path.GetFileName(path);
        ModelDetails.ToolTip = path ?? settings.ModelFolder;
        DownloadButton.Content = path == null ? "Download selected model" : "Model installed";
        DownloadButton.IsEnabled = cancellation == null && path == null;
    }
    void ModelChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (refreshingModels || settings == null || cancellation != null || ModelList.SelectedValue is not string name) return;
        var previous = settings.Model;
        try { settings.Model = name; settings.Save(); UpdateSelectedModel(); }
        catch (Exception ex) { settings.Model = previous; RefreshModels(); Error(ex); }
    }
    async void DownloadSelectedModel(object sender, RoutedEventArgs e)
    {
        if (cancellation != null) return;
        try
        {
            cancellation = new(); Busy(true);
            StatusLabel.Text = "Downloading " + settings.Model + "...";
            await ModelDownload.Download(settings.Model, settings.ModelFolder, new Progress<string>(line => StatusLabel.Text = line), cancellation.Token);
            StatusLabel.Text = "Model installed and SHA-256 verified: " + settings.Model;
        }
        catch (OperationCanceledException) { StatusLabel.Text = "Model download cancelled"; }
        catch (Exception ex) { StatusLabel.Text = "Model download failed"; Error(ex); }
        finally { cancellation?.Dispose(); cancellation = null; Busy(false); RefreshModels(); }
    }
    void UpdateProgress()
    {
        while (messages.TryDequeue(out var line))
        {
            var match = Regex.Match(line, @"progress\s*=\s*(\d+)%");
            if (match.Success) { Progress.IsIndeterminate = false; Progress.Value = int.Parse(match.Groups[1].Value); StatusLabel.Text = $"Transcribing: {match.Groups[1].Value}%"; }
            if (line.StartsWith("Converting")) StatusLabel.Text = "Preparing audio...";
            if (line.StartsWith("Starting")) StatusLabel.Text = "whisper.cpp: " + (settings.UseCuda ? "Verifying CUDA..." : "CPU");
            if (line.StartsWith("NVIDIA process verified")) StatusLabel.Text = $"CUDA verified · {Progress.Value:F0}%";
        }
        if (cancellation != null) MetricsLabel.Text = $"Elapsed: {watch.Elapsed:hh\\:mm\\:ss} · {Path.GetFileName(InputBox.Text)}";
    }
    void Error(Exception ex) => MessageBox.Show(this, ex.Message, "Speech to Text", MessageBoxButton.OK, MessageBoxImage.Error);
    bool Discard() => !dirty || MessageBox.Show(this, "Discard unsaved changes?", "Transcript", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    void Browse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Media|*.mp3;*.wav;*.m4a;*.mp4;*.mkv" };
        if (dialog.ShowDialog(this) == true) InputBox.Text = dialog.FileName;
    }
    void FileDrop(object sender, DragEventArgs e)
    {
        if (cancellation == null && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1) InputBox.Text = files[0];
    }
    async void Start(object sender, RoutedEventArgs e) => await Transcribe();
    public async Task Transcribe(string? benchmarkInput = null)
    {
        if (cancellation != null || !Discard()) return;
        try
        {
            if (benchmarkInput != null) InputBox.Text = benchmarkInput;
            var input = Path.GetFullPath(InputBox.Text.Trim().Trim('"'));
            if (!File.Exists(input) || !Transcriber.Supported(input)) throw new ArgumentException("Enter the path to an MP3, WAV, M4A, MP4, or MKV file.");
            if (!File.Exists(settings.WhisperPath) || settings.ModelPath(settings.Model) == null)
            {
                MessageBox.Show(this, "First-time setup: open Settings, install the transcription tools, and download your selected model. Then click Transcribe again.", "Setup required");
                ShowSettings(this, new RoutedEventArgs());
                return;
            }
            cancellation = new();
            Busy(true);
            StatusLabel.Text = "Starting...";
            var folder = Path.Combine(Settings.ResultsFolder, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(folder);
            var progress = new Progress<string>(line => { messages.Enqueue(line); File.AppendAllText(Path.Combine(folder, "backend.log"), line + Environment.NewLine); });
            var result = await new Transcriber().RunAsync(input, settings, folder, progress, cancellation.Token);
            await File.WriteAllTextAsync(Path.Combine(folder, "benchmark.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            var transcript = await File.ReadAllTextAsync(result.TxtPath);
            TranscriptBox.Text = transcript;
            txtPath = result.TxtPath;
            dirty = false;
            UpdateProgress();
            Progress.IsIndeterminate = false; Progress.Value = 100;
            StatusLabel.Text = "Done. TXT saved automatically: " + result.TxtPath;
            MetricsLabel.Text = $"Audio: {TimeSpan.FromSeconds(result.AudioSeconds):hh\\:mm\\:ss} · Transcription: {result.TranscriptionSeconds:F2}s · RTF: {result.RealtimeFactor:F3} · {result.Backend}";

        }
        catch (OperationCanceledException) { UpdateProgress(); StatusLabel.Text = "Cancelled"; }
        catch (Exception ex) { UpdateProgress(); StatusLabel.Text = "Error"; Error(ex); }
        finally { cancellation?.Dispose(); cancellation = null; Busy(false); }
    }
    void Busy(bool busy)
    {
        StartButton.IsEnabled = BrowseButton.IsEnabled = MoreButton.IsEnabled = InputBox.IsEnabled = !busy;
        ModelList.IsEnabled = !busy;
        DownloadButton.IsEnabled = !busy && settings.ModelPath(settings.Model) == null;
        TranscriptBox.IsReadOnly = busy;
        CancelButton.IsEnabled = busy;
        SaveButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(TranscriptBox.Text);
        Progress.IsIndeterminate = busy;
        if (busy) { Progress.Value = 0; watch.Restart(); } else watch.Stop();
    }
    void Cancel(object sender, RoutedEventArgs e) { StatusLabel.Text = "Cancelling..."; cancellation?.Cancel(); }
    void TextEdited(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        dirty = true;
        if (SaveButton != null) SaveButton.IsEnabled = cancellation == null && !string.IsNullOrWhiteSpace(TranscriptBox.Text);
    }
    void SaveText(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SaveFileDialog { Filter = "Text|*.txt", FileName = txtPath == null ? "transcript.txt" : Path.GetFileName(txtPath) };
            if (dialog.ShowDialog(this) == true) { SubtitleFiles.AtomicWrite(dialog.FileName, TranscriptBox.Text); dirty = false; StatusLabel.Text = "Saved: " + dialog.FileName; }
        }
        catch (Exception ex) { Error(ex); }
    }
    void ShowSettings(object sender, RoutedEventArgs e)
    {
        new SettingsWindow(settings) { Owner = this }.ShowDialog();
        RefreshModels();
    }
    void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (cancellation != null) { e.Cancel = true; MessageBox.Show(this, "Cancel the current transcription before closing."); return; }
        if (!Discard()) e.Cancel = true; else timer.Stop();
    }
}
