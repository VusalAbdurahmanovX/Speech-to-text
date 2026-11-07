using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SubtitleStudio;

public sealed class SubtitleLine
{
    public string Start { get; set; } = "00:00:00,000";
    public string End { get; set; } = "00:00:00,000";
    public string Text { get; set; } = "";
}

public static class SubtitleFiles
{
    public static TimeSpan Time(string value)
    {
        var m = Regex.Match(value, @"^(\d{2,}):([0-5]\d):([0-5]\d)[,.](\d{3})$");
        if (!m.Success) throw new FormatException($"Invalid timestamp: {value}. Use HH:mm:ss,fff.");
        return TimeSpan.FromHours(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) +
            TimeSpan.FromMinutes(int.Parse(m.Groups[2].Value)) + TimeSpan.FromSeconds(int.Parse(m.Groups[3].Value)) + TimeSpan.FromMilliseconds(int.Parse(m.Groups[4].Value));
    }

    public static List<SubtitleLine> Read(string path)
    {
        var blocks = Regex.Split(File.ReadAllText(path).Trim().Replace("\r\n", "\n"), @"\n\s*\n");
        return blocks.Where(b => !string.IsNullOrWhiteSpace(b)).Select(b =>
        {
            var lines = b.Split('\n');
            var i = Array.FindIndex(lines, l => l.Contains(" --> "));
            if (i < 0) throw new FormatException("Invalid SRT block.");
            var times = lines[i].Split(" --> ");
            return new SubtitleLine { Start = times[0].Trim(), End = times[1].Trim(), Text = string.Join("\n", lines.Skip(i + 1)) };
        }).ToList();
    }

    public static string Srt(IEnumerable<SubtitleLine> source)
    {
        var result = new StringBuilder();
        int index = 0;
        foreach (var line in source)
        {
            if (Time(line.End) <= Time(line.Start)) throw new FormatException($"Subtitle {index + 1}: end must be after start.");
            if (string.IsNullOrWhiteSpace(line.Text)) throw new FormatException($"Subtitle {index + 1}: text is empty.");
            result.AppendLine((++index).ToString()).AppendLine($"{line.Start.Replace('.', ',')} --> {line.End.Replace('.', ',')}").AppendLine(line.Text.Trim()).AppendLine();
        }
        return result.ToString();
    }

    public static void AtomicWrite(string path, string text)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
