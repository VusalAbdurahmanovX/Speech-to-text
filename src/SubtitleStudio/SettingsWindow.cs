using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace SubtitleStudio;

public partial class SettingsWindow : Window
{
    readonly Settings settings;
    CancellationTokenSource? cancellation;

    public SettingsWindow(Settings settings)
    {
        this.settings = settings;
        InitializeComponent();
        LanguageBox.ItemsSource = new[] { "tr", "az", "en", "auto", "ru", "de", "fr", "es", "ar", "it", "ja", "zh", "uk" };
        LanguageBox.Text = settings.Language;
        ModelBox.ItemsSource = Transcriber.Models;
        ModelBox.SelectedItem = settings.Model;
        CudaButton.IsChecked = settings.UseCuda;
        CpuButton.IsChecked = !settings.UseCuda;
        WhisperPathBox.Text = settings.WhisperPath;
        FFmpegPathBox.Text = settings.FFmpegPath;
        ModelFolderBox.Text = settings.ModelFolder;
        BeamBox.Text = settings.BeamSize.ToString();
        ThreadsBox.Text = settings.Threads.ToString();
        LayersBox.Text = settings.GpuLayers.ToString();
        RefreshModel();
        if (!File.Exists(settings.WhisperPath)) SettingsTabs.SelectedIndex = 1;
    }

    void RefreshModel()
    {
        if (ModelInfoLabel == null || ModelBox.SelectedItem is not string name) return;
        try
        {
            var path = new Settings { ModelFolder = ModelFolderBox.Text }.ModelPath(name);
            ModelInfoLabel.Text = path == null ? "Not installed in the selected folder" : $"Installed - {new FileInfo(path).Length / 1048576} MiB";
            ModelInfoLabel.ToolTip = path;
            DownloadButton.Content = path == null ? "Download selected model" : "Model installed";
            DownloadButton.IsEnabled = cancellation == null && path == null;
        }
        catch (Exception ex) { StatusLabel.Text = ex.Message; DownloadButton.IsEnabled = false; }
    }
    void SelectedModelChanged(object sender, SelectionChangedEventArgs e) => RefreshModel();
    void ModelFolderChanged(object sender, TextChangedEventArgs e) => RefreshModel();

    void Save()
    {
        if (!int.TryParse(BeamBox.Text, out var beam) || beam is < 1 or > 100 ||
            !int.TryParse(ThreadsBox.Text, out var threads) || threads is < 1 or > 256 ||
            !int.TryParse(LayersBox.Text, out var layers) || layers is < 1 or > 999)
            throw new ArgumentException("Beam size: 1-100; threads: 1-256; GPU layers: 1-999.");
        var code = LanguageBox.Text.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(code, "^(auto|[a-z]{2,3})$")) throw new ArgumentException("Enter a language code such as tr, az, en, or auto.");
        if (string.IsNullOrWhiteSpace(WhisperPathBox.Text) || string.IsNullOrWhiteSpace(FFmpegPathBox.Text) || string.IsNullOrWhiteSpace(ModelFolderBox.Text))
            throw new ArgumentException("All tool paths and the model folder are required.");
        var whisper = Path.GetFullPath(WhisperPathBox.Text.Trim());
        var modelFolder = Path.GetFullPath(ModelFolderBox.Text.Trim());
        if (ModelBox.SelectedItem is not string model || !Transcriber.Models.Contains(model)) throw new ArgumentException("Select a supported Whisper model.");
        settings.WhisperPath = whisper;
        settings.FFmpegPath = FFmpegPathBox.Text.Trim();
        settings.ModelFolder = modelFolder;
        settings.Model = model;
        settings.Language = code;
        settings.UseCuda = CudaButton.IsChecked == true;
        settings.BeamSize = beam; settings.Threads = threads; settings.GpuLayers = layers;
        settings.Save();
    }
    void SaveSettings(object sender, RoutedEventArgs e)
    {
        try { Save(); RefreshModel(); StatusLabel.Text = "Settings saved."; } catch (Exception ex) { Error(ex); }
    }
    async Task Download(Func<CancellationToken, Task> operation)
    {
        if (cancellation != null) return;
        try
        {
            cancellation = new();
            SettingsTabs.IsEnabled = SaveButton.IsEnabled = CloseButton.IsEnabled = false;
            CancelButton.Visibility = OperationProgress.Visibility = Visibility.Visible;
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException) { StatusLabel.Text = "Download cancelled."; }
        catch (Exception ex) { StatusLabel.Text = "Download failed."; Error(ex); }
        finally
        {
            cancellation?.Dispose(); cancellation = null;
            SettingsTabs.IsEnabled = SaveButton.IsEnabled = CloseButton.IsEnabled = true;
            CancelButton.Visibility = OperationProgress.Visibility = Visibility.Collapsed;
            RefreshModel();
        }
    }
    async void InstallTools(object sender, RoutedEventArgs e) => await Download(async token =>
    {
        var paths = await ToolDownload.Install(new Progress<string>(line => StatusLabel.Text = line), token);
        WhisperPathBox.Text = paths.Whisper;
        FFmpegPathBox.Text = paths.FFmpeg;
        Save();
        StatusLabel.Text = "Tools installed. Select and download a model in General.";
    });
    async void DownloadModel(object sender, RoutedEventArgs e) => await Download(async token =>
    {
        Save();
        await ModelDownload.Download(settings.Model, settings.ModelFolder, new Progress<string>(line => StatusLabel.Text = line), token);
        StatusLabel.Text = "Model installed and SHA-256 verified.";
    });
    void CancelOperation(object sender, RoutedEventArgs e) { cancellation?.Cancel(); StatusLabel.Text = "Cancelling..."; }
    void BrowseWhisper(object sender, RoutedEventArgs e) => BrowseExe(WhisperPathBox);
    void BrowseFFmpeg(object sender, RoutedEventArgs e) => BrowseExe(FFmpegPathBox);
    void BrowseExe(TextBox box)
    {
        var dialog = new OpenFileDialog { Filter = "Executable|*.exe" };
        if (dialog.ShowDialog(this) == true) box.Text = dialog.FileName;
    }
    void BrowseModels(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(this) == true) ModelFolderBox.Text = dialog.FolderName;
    }
    async void Benchmark(object sender, RoutedEventArgs e)
    {
        try
        {
            Save();
            var input = Path.Combine(Settings.Root, "sample_1min.mp3");
            if (!File.Exists(input)) throw new FileNotFoundException("Place sample_1min.mp3 in the application root before running the benchmark.");
            var owner = (MainWindow)Owner;
            Close();
            await owner.Transcribe(input);
        }
        catch (Exception ex) { Error(ex); }
    }
    void CloseSettings(object sender, RoutedEventArgs e) => Close();
    void WindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (cancellation == null) return;
        e.Cancel = true; cancellation.Cancel(); StatusLabel.Text = "Cancelling download. Close when it finishes.";
    }
    void Error(Exception ex) => MessageBox.Show(this, ex.Message, "Settings", MessageBoxButton.OK, MessageBoxImage.Error);
}
