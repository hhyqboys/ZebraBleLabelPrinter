using System.IO;
using System.Text.Json;
using ZebraBleLabelPrinter.Models;

namespace ZebraBleLabelPrinter.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private string GetPath()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZebraPrinter");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    public PrintSettings Load()
    {
        try
        {
            string path = GetPath();
            if (!File.Exists(path)) return new PrintSettings();
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PrintSettings>(json) ?? new PrintSettings();
        }
        catch
        {
            return new PrintSettings();
        }
    }

    public void Save(PrintSettings s)
    {
        try
        {
            string path = GetPath();
            File.WriteAllText(path, JsonSerializer.Serialize(s, JsonOpts));
        }
        catch { /* 配置写失败不影响主流程 */ }
    }
}
