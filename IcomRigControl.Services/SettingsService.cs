using System.Text.Json;

namespace IcomRigControl.Services;

/// <summary>
/// Loads and saves AppSettings to a local JSON file. Never throws — a
/// missing or corrupt settings file falls back to defaults rather than
/// blocking app startup. See CLAUDE.md's credential-storage rule: this
/// file must never be committed to source control.
/// </summary>
public class SettingsService
{
    private readonly string _filePath;

    // IncludeFields is REQUIRED: N1mmDestinations is a List<(string Ip, int Port)> and
    // ValueTuple exposes Ip/Port as public FIELDS (Item1/Item2), which System.Text.Json
    // ignores by default — without this every saved destination round-trips as
    // ("", 0). Serialize and deserialize must use the SAME options.
    private static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true, IncludeFields = true };

    public SettingsService(string filePath)
    {
        _filePath = filePath;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            // Corrupt file, permissions issue, etc. — never block startup,
            // just fall back to defaults.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}