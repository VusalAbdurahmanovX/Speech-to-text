using SubtitleStudio;
using System.Diagnostics;
using System.IO;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Reject(Action action)
{
    try { action(); } catch (FormatException) { return; }
    throw new Exception("Expected timestamp validation to fail.");
}
var folder = Path.Combine(Path.GetTempPath(), "SubtitleStudio-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    var path = Path.Combine(folder, "subtitle.srt");
    var text = "Salam, dünya!\nSecond line.";
    SubtitleFiles.AtomicWrite(path, SubtitleFiles.Srt([new() { Start = "00:00:01.250", End = "00:00:02,500", Text = text }]));
    var rows = SubtitleFiles.Read(path);
    Check(rows.Count == 1 && rows[0].Text == text && rows[0].Start == "00:00:01,250", "SRT Unicode/multiline roundtrip failed.");
    Check(SubtitleFiles.Time("25:00:00,000").TotalHours == 25, "Long timestamp failed.");
    Reject(() => SubtitleFiles.Time("00:60:00,000"));
    Reject(() => SubtitleFiles.Srt([new() { Start = "00:00:03,000", End = "00:00:02,000", Text = "Invalid" }]));
    File.WriteAllText(Path.Combine(folder, "ggml-large-v3-turbo-q5_0.bin"), "test");
    var settings = new Settings { ModelFolder = folder };
    Check(settings.ModelPath("large-v3") == null, "large-v3 must not resolve to turbo.");
    Check(settings.ModelPath("large-v3-turbo") != null, "Quantized model discovery failed.");
    var officialFiles = new[] { "ggml-tiny-q5_1.bin", "ggml-tiny.bin", "ggml-medium-q5_0.bin", "ggml-large-v1.bin", "ggml-large-v2-q5_0.bin", "ggml-large-v2.bin", "ggml-large-v3-q5_0.bin", "ggml-large-v3-turbo-q5_0.bin" };
    Check(ModelDownload.SelectFile("tiny", officialFiles) == "ggml-tiny-q5_1.bin", "Tiny must use its available Q5_1 download.");
    Check(ModelDownload.SelectFile("large-v1", officialFiles) == "ggml-large-v1.bin", "Large-v1 must use the standard model when quantized files are unavailable.");
    Check(ModelDownload.SelectFile("large-v2", officialFiles) == "ggml-large-v2-q5_0.bin", "Large-v2 should prefer Q5_0.");
    foreach (var model in Transcriber.Models)
    {
        var name = ModelDownload.SelectFile(model, officialFiles);
        File.WriteAllText(Path.Combine(folder, name), "test");
        Check(Path.GetFileName(settings.ModelPath(model)) == name, "Model discovery chose the wrong family: " + model);
    }
    var marker = Path.Combine(folder, "should-not-exist");
    var literal = "hello; New-Item '" + marker + "'";
    var script = Path.Combine(folder, "echo.ps1");
    File.WriteAllText(script, "param([string]$Value)\nWrite-Output $Value");
    var output = await ProcessRunner.Run("powershell.exe", ["-NoProfile", "-File", script, "-Value", literal], CancellationToken.None);
    Check(output.Contains(literal) && !File.Exists(marker), "Arguments were interpreted as shell code.");
    using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    int pid = 0;
    try
    {
        await ProcessRunner.Run("powershell.exe", ["-NoProfile", "-Command", "Start-Sleep -Seconds 30"], cancel.Token, started: id => pid = id);
        throw new Exception("Cancellation failed.");
    }
    catch (OperationCanceledException) { }
    try { using var process = Process.GetProcessById(pid); Check(process.HasExited, "Cancelled process still running."); }
    catch (ArgumentException) { }
    Console.WriteLine("PASS: SRT, timestamp validation, model discovery, literal process arguments, cancellation.");
    Check(Settings.IsSourceCheckout, "Source checkout root discovery failed.");
    Check(Path.GetFullPath(Settings.ResultsFolder) == Path.Combine(Settings.Root, "artifacts"), "Development results folder changed.");
    Check(Transcriber.Supported("VOICE.MP3") && !Transcriber.Supported("script.exe"), "Media extension validation failed.");
    using (var iconStream = File.OpenRead(Path.Combine(Settings.Root, "src", "SubtitleStudio", "Assets", "AppIcon.ico")))
    {
        var icon = new System.Windows.Media.Imaging.IconBitmapDecoder(iconStream, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        Check(icon.Frames.Count == 7 && icon.Frames.Any(f => f.PixelWidth == 16) && icon.Frames.Any(f => f.PixelWidth == 256), "Windows icon is missing small or high-resolution frames.");
    }
    if (args.Contains("--ui"))
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                var window = new MainWindow();
                window.Show(); window.UpdateLayout();
                Check(window.Title.StartsWith("Speech to Text"), "Window product name is incorrect.");
                Check(window.Icon != null, "App window icon was not loaded.");
                var list = (System.Windows.Controls.ListBox)window.FindName("ModelList");
                Check(list.Items.Count == Transcriber.Models.Length && list.Items.Count == 6, "Model library must display all six supported families.");
                Check(list.SelectedValue is string selected && Transcriber.Models.Contains(selected), "Selected model is missing.");
                var button = (System.Windows.Controls.Button)window.FindName("DownloadButton");
                Check(button.IsEnabled == (Settings.Load().ModelPath((string)list.SelectedValue) == null), "Download availability does not match installed model state.");
                Check(!((System.Windows.Controls.Button)window.FindName("SaveButton")).IsEnabled, "Empty transcript must not be exportable.");
                void Snapshot(System.Windows.Window target, string name)
                {
                    target.UpdateLayout();
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(target);
                    var image = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    image.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    Directory.CreateDirectory(Settings.ResultsFolder);
                    using var file = File.Create(Path.Combine(Settings.ResultsFolder, name + ".png"));
                    image.Save(file);
                }
                Snapshot(window, "ui-preview");
                var preferences = Settings.Load();
                var settingsWindow = new SettingsWindow(preferences) { Owner = window };
                settingsWindow.Show();
                var tabs = (System.Windows.Controls.TabControl)settingsWindow.FindName("SettingsTabs");
                Check(tabs.Items.Count == 3, "Settings must have General, Tools, and Advanced sections.");
                Check(((System.Windows.Controls.ComboBox)settingsWindow.FindName("LanguageBox")).Text == preferences.Language, "Settings language was not loaded.");
                Check(((System.Windows.Controls.RadioButton)settingsWindow.FindName("CudaButton")).IsChecked == preferences.UseCuda, "Backend preference was not loaded.");
                Check(((System.Windows.Controls.TextBox)settingsWindow.FindName("BeamBox")).Text == preferences.BeamSize.ToString(), "Beam preference was not loaded.");
                for (int index = 0; index < tabs.Items.Count; index++)
                {
                    tabs.SelectedIndex = index;
                    Snapshot(settingsWindow, "settings-" + index);
                }
                settingsWindow.Close();
                window.Close(); app.Shutdown();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (failure != null) throw failure;
        Console.WriteLine("PASS: WPF models, transcript state, settings tabs/preferences, rendered previews.");
    }
    if (args.Contains("--tiny-smoke"))
    {
        var preferences = Settings.Load();
        preferences.Model = "tiny";
        preferences.UseCuda = true;
        var progress = new Progress<string>(Console.WriteLine);
        if (preferences.ModelPath("tiny") == null)
            await ModelDownload.Download("tiny", preferences.ModelFolder, progress, CancellationToken.None);
        var outputFolder = Path.Combine(Settings.ResultsFolder, "tiny-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputFolder);
        var clip = Path.Combine(outputFolder, "tiny-sample.wav");
        await ProcessRunner.Run(preferences.FFmpegPath, ["-nostdin", "-hide_banner", "-y", "-i", Path.Combine(Settings.Root, "sample_1min.mp3"), "-t", "90", "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", clip], CancellationToken.None);
        var result = await new Transcriber().RunAsync(clip, preferences, outputFolder, progress, CancellationToken.None);
        Check(result.Backend == "CUDA" && result.NvidiaProcessVerified && File.Exists(result.TxtPath), "Tiny CUDA smoke test failed.");
        Console.WriteLine($"PASS: tiny CUDA, audio={result.AudioSeconds:F2}s, transcription={result.TranscriptionSeconds:F2}s, RTF={result.RealtimeFactor:F4}");
    }
    if (args.Contains("--install-tools"))
    {
        var paths = await ToolDownload.Install(new Progress<string>(Console.WriteLine), CancellationToken.None);
        Check((await ProcessRunner.Run(paths.Whisper, ["--help"], CancellationToken.None)).Contains("--output-txt"), "Installed whisper CLI did not start.");
        Check((await ProcessRunner.Run(paths.FFmpeg, ["-version"], CancellationToken.None)).Contains("ffmpeg version"), "Installed FFmpeg did not start.");
        Console.WriteLine("PASS: upstream tool installation, SHA-256 verification, extraction, native executable startup.");
    }
}
finally { Directory.Delete(folder, true); }
