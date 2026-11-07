using System.IO;
using System.Windows;
using System.Text.Json;

namespace SubtitleStudio;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && e.Args[0] == "--benchmark")
        {
            try
            {
                var settings = Settings.Load();
                var input = e.Args.Length > 1 ? Path.GetFullPath(e.Args[1]) : Path.Combine(Settings.Root, "sample_1min.mp3");
                var folder = Path.Combine(Settings.ResultsFolder, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
                Directory.CreateDirectory(folder);
                var log = new Progress<string>(line => File.AppendAllText(Path.Combine(folder, "backend.log"), line + Environment.NewLine));
                var result = await new Transcriber().RunAsync(input, settings, folder, log, CancellationToken.None);
                await File.WriteAllTextAsync(Path.Combine(folder, "benchmark.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(Settings.ResultsFolder);
                File.WriteAllText(Path.Combine(Settings.ResultsFolder, "benchmark-error.txt"), ex.ToString());
                Shutdown(1);
            }
            return;
        }
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
