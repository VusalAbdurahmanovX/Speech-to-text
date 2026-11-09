using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SubtitleStudio;

public sealed record TranscriptionResult(string Input, string Model, string Backend, double AudioSeconds, double TranscriptionSeconds, double RealtimeFactor, string SrtPath, string TxtPath, bool NvidiaProcessVerified);

public sealed class Transcriber
{
    public static readonly string[] Models = ["tiny", "medium", "large-v1", "large-v2", "large-v3", "large-v3-turbo"];
    public static bool Supported(string path) => new[] { ".mp4", ".mkv", ".mp3", ".wav", ".m4a" }.Contains(Path.GetExtension(path).ToLowerInvariant());

    public async Task<TranscriptionResult> RunAsync(string input, Settings settings, string outputFolder, IProgress<string> progress, CancellationToken token)
    {
        if (!File.Exists(input) || !Supported(input)) throw new ArgumentException("Choose an existing MP4, MKV, MP3, WAV, or M4A file.");
        if (!Models.Contains(settings.Model)) throw new ArgumentException("Unknown model.");
        if (!Regex.IsMatch(settings.Language, "^(auto|[a-z]{2,3})$")) throw new ArgumentException("Use a language code such as tr, az, en, or auto.");
        if (settings.BeamSize is < 1 or > 100 || settings.Threads < 1 || settings.Threads > 256 || settings.GpuLayers is < 1 or > 999)
            throw new ArgumentException("Beam size must be 1-100, threads 1-256, GPU layers 1-999.");
        var model = settings.ModelPath(settings.Model) ?? throw new FileNotFoundException($"Install {settings.Model} in {settings.ModelFolder} first.");
        if (!File.Exists(settings.WhisperPath)) throw new FileNotFoundException("Set the whisper-cli.exe path in Settings.");
        Directory.CreateDirectory(outputFolder);
        progress.Report("Inspecting installed whisper-cli.exe --help...");
        var help = await ProcessRunner.Run(settings.WhisperPath, ["--help"], token);
        await File.WriteAllTextAsync(Path.Combine(outputFolder, "whisper-help.txt"), help, token);
        bool Has(string flag) => Regex.IsMatch(help, @"(?<![\w-])" + Regex.Escape(flag) + @"(?=\s|,|$)");
        string Require(string flag) => Has(flag) ? flag : throw new InvalidOperationException($"Installed whisper.cpp does not support {flag}. See saved whisper-help.txt.");
        foreach (var flag in new[] { "--model", "--file", "--language", "--output-srt", "--output-txt", "--output-file", "--threads", "--beam-size" }) Require(flag);
        if (settings.UseCuda)
        {
            progress.Report("Checking NVIDIA driver...");
            try { progress.Report(await ProcessRunner.Run("nvidia-smi.exe", ["--query-gpu=name,driver_version", "--format=csv,noheader"], token)); }
            catch (Exception ex) when (ex is not OperationCanceledException) { throw new InvalidOperationException("CUDA requested but nvidia-smi failed. Install a working NVIDIA driver. CPU fallback is disabled.", ex); }
        }
        var temp = Path.Combine(Path.GetTempPath(), "SubtitleStudio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var wav = input;
            if (!IsPcm16Mono(input))
            {
                progress.Report("Converting audio to 16 kHz mono PCM WAV...");
                wav = Path.Combine(temp, "audio.wav");
                await ProcessRunner.Run(settings.FFmpegPath, ["-nostdin", "-hide_banner", "-y", "-i", input, "-map", "0:a:0", "-vn", "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav], token, progress.Report);
            }
            var duration = WavDuration(wav);
            progress.Report($"Audio duration: {duration:F2} seconds");
            var output = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(input));
            var args = new List<string> { "--model", model, "--file", wav, "--language", settings.Language, "--output-srt", "--output-txt", "--output-file", output, "--threads", settings.Threads.ToString(), "--beam-size", settings.BeamSize.ToString() };
            if (Has("--print-progress")) args.Add("--print-progress");
            if (!settings.UseCuda) args.Add(Require("--no-gpu"));
            // ponytail: stock whisper.cpp offloads the full encoder; pass a layer count only if this build exposes it.
            var layersFlag = new[] { "--gpu-layers", "--n-gpu-layers" }.FirstOrDefault(Has);
            if (settings.UseCuda && layersFlag != null) { args.Add(layersFlag); args.Add(settings.GpuLayers.ToString()); }
            else if (settings.UseCuda) progress.Report("This whisper.cpp build has no GPU-layer flag; it manages CUDA offload automatically. Saved layer count is not applied.");

            using var backendCancel = CancellationTokenSource.CreateLinkedTokenSource(token);
            using var monitorCancel = new CancellationTokenSource();
            int cudaEvidence = 0, pidSeen = 0;
            bool rejectedCpu = false;
            Task monitor = Task.CompletedTask;
            var watch = Stopwatch.StartNew();
            progress.Report(settings.UseCuda ? "Starting CUDA transcription; verifying backend..." : "Starting CPU transcription (explicit selection)...");
            try
            {
                await ProcessRunner.Run(settings.WhisperPath, args, backendCancel.Token, line =>
                {
                    progress.Report(line);
                    if (Regex.IsMatch(line, @"CUDA\d*.*(?:compute buffer|model buffer|using CUDA)|using CUDA\d* backend", RegexOptions.IgnoreCase)) Interlocked.Exchange(ref cudaEvidence, 1);
                    if (settings.UseCuda && line.Contains("main: processing") && Volatile.Read(ref cudaEvidence) == 0)
                    {
                        rejectedCpu = true;
                        backendCancel.Cancel();
                    }
                }, pid =>
                {
                    if (settings.UseCuda) monitor = MonitorGpu(pid, outputFolder, () => Interlocked.Exchange(ref pidSeen, 1), progress, monitorCancel.Token);
                });
            }
            catch (OperationCanceledException) when (rejectedCpu && !token.IsCancellationRequested)
            { throw new InvalidOperationException("CUDA requested, but whisper.cpp did not allocate CUDA buffers. Transcription stopped; CPU fallback is disabled. Check the CUDA build and its DLLs."); }
            finally
            {
                watch.Stop();
                monitorCancel.Cancel();
                await monitor;
            }
            if (settings.UseCuda && (cudaEvidence == 0 || pidSeen == 0))
                throw new InvalidOperationException("CUDA could not be verified from both whisper.cpp CUDA buffers and its process in nvidia-smi. Output is not accepted as a verified CUDA run. See backend and nvidia-smi logs.");
            // whisper.cpp flushes the two optional outputs just after the main process exits on some builds.
            var srtPath = output + ".srt";
            var txtPath = output + ".txt";
            for (var attempt = 0; attempt < 20 && (!File.Exists(srtPath) || !File.Exists(txtPath)); attempt++)
                await Task.Delay(100, token);
            if (!File.Exists(srtPath) || !File.Exists(txtPath))
            {
                var files = string.Join(", ", Directory.EnumerateFiles(outputFolder).Select(Path.GetFileName));
                throw new IOException($"whisper.cpp did not generate SRT and TXT outputs. Files found: {files}");
            }
            return new(input, model, settings.UseCuda ? "CUDA" : "CPU", duration, watch.Elapsed.TotalSeconds, watch.Elapsed.TotalSeconds / duration, srtPath, txtPath, pidSeen != 0);
        }
        finally { Directory.Delete(temp, true); }
    }

    static async Task MonitorGpu(int pid, string folder, Action verified, IProgress<string> progress, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var rows = await ProcessRunner.Run("nvidia-smi.exe", ["--query-compute-apps=pid,process_name,used_gpu_memory", "--format=csv,noheader"], token);
                var stats = await ProcessRunner.Run("nvidia-smi.exe", ["--query-gpu=timestamp,name,utilization.gpu,memory.used", "--format=csv,noheader"], token);
                await File.AppendAllTextAsync(Path.Combine(folder, "nvidia-smi.log"), $"{DateTimeOffset.Now:O} whisper PID={pid}\n{stats}{rows}\n", token);
                if (rows.Split('\n').Any(row => row.Split(',')[0].Trim() == pid.ToString(CultureInfo.InvariantCulture)))
                {
                    verified();
                    progress.Report($"NVIDIA process verified: whisper-cli.exe PID {pid}");
                }
                await Task.Delay(1500, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { progress.Report("NVIDIA verification failed: " + ex.Message); }
    }

    static bool IsPcm16Mono(string path)
    {
        if (!Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase)) return false;
        try { return ReadWav(path).valid; } catch (IOException) { return false; }
    }
    static double WavDuration(string path)
    {
        var wav = ReadWav(path);
        if (!wav.valid || wav.seconds <= 0) throw new IOException("Audio is not a valid, nonempty 16 kHz mono PCM16 WAV.");
        return wav.seconds;
    }
    static (bool valid, double seconds) ReadWav(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        string Four() => System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (Four() != "RIFF") return (false, 0);
        reader.ReadUInt32();
        if (Four() != "WAVE") return (false, 0);
        bool valid = false;
        uint data = 0;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            var id = Four();
            var size = reader.ReadUInt32();
            var next = reader.BaseStream.Position + size + (size % 2);
            if (next > reader.BaseStream.Length) return (false, 0);
            if (id == "fmt " && size >= 16)
            {
                var format = reader.ReadUInt16(); var channels = reader.ReadUInt16(); var rate = reader.ReadUInt32();
                reader.ReadUInt32(); reader.ReadUInt16(); var bits = reader.ReadUInt16();
                valid = format == 1 && channels == 1 && rate == 16000 && bits == 16;
            }
            if (id == "data") data = size;
            reader.BaseStream.Position = next;
        }
        return (valid, data / 32000.0);
    }
}
