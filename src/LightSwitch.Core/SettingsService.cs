using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LightSwitch.Core;

/// <summary>
/// Loads and atomically saves <see cref="AppSettings"/>.
/// Also imports the config.json that the original standalone build kept next
/// to its executable, but only when no per-user configuration exists yet.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public SettingsService(string? configPath = null)
    {
        ConfigPath = configPath ?? AppPaths.ConfigPath;
    }

    public string ConfigPath { get; }

    public AppSettings Current { get; private set; } = new();

    /// <summary>Exact JSON text this process last read or wrote, used to ignore our own file-change notifications.</summary>
    public string? LastJson { get; private set; }

    public bool ImportedLegacyConfig { get; private set; }

    public string? LastError { get; private set; }

    public AppSettings Load()
    {
        LastError = null;
        ImportedLegacyConfig = false;

        var source = ConfigPath;
        if (!File.Exists(source))
        {
            if (File.Exists(AppPaths.LegacyConfigPath))
            {
                source = AppPaths.LegacyConfigPath;
                ImportedLegacyConfig = true;
            }
            else
            {
                Current = new AppSettings();
                LastJson = null;
                return Current;
            }
        }

        try
        {
            var json = File.ReadAllText(source, Encoding.UTF8);
            Current = Deserialize(json);
            LastJson = json;
            if (ImportedLegacyConfig)
            {
                Log.Info($"Imported legacy configuration from {source}");
                // Complete the migration so the next launch uses the new location even
                // when the user never changes a setting.
                if (!Save(Current))
                {
                    LastJson = null;
                    Save(Current);
                }
            }

            return Current;
        }
        catch (Exception ex)
        {
            LastError = $"无法读取配置文件：{ex.Message}";
            Log.Error($"Failed to read {source}", ex);
            TryBackupCorruptFile(source);
            Current = new AppSettings();
            LastJson = null;
            return Current;
        }
    }

    /// <summary>Serializes and writes atomically. Returns false when the content did not change.</summary>
    public bool Save(AppSettings settings)
    {
        settings.Normalize();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        if (string.Equals(json, LastJson, StringComparison.Ordinal))
        {
            Current = settings;
            return false;
        }

        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = ConfigPath + ".tmp";
        try
        {
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, ConfigPath, overwrite: true);
            LastJson = json;
            Current = settings;
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"无法保存配置文件：{ex.Message}";
            Log.Error($"Failed to write {ConfigPath}", ex);
            try
            {
                File.WriteAllText(ConfigPath, json, new UTF8Encoding(false));
                LastJson = json;
                Current = settings;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static AppSettings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        settings.Normalize();
        return settings;
    }

    private static void TryBackupCorruptFile(string path)
    {
        try
        {
            var backup = $"{path}.bad-{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(path, backup, overwrite: true);
            Log.Warn($"Backed up unreadable configuration to {backup}");
        }
        catch
        {
            // Best effort only.
        }
    }
}
