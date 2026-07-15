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
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
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

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}