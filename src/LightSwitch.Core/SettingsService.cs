using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LightSwitch.Core;

/// <summary>Outcome of <see cref="SettingsService.Save"/>: a no-op save is not a failure.</summary>
public enum SaveResult
{
    Saved,
    Unchanged,
    Failed,
}

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
                if (Save(Current) == SaveResult.Unchanged)
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

    /// <summary>
    /// Serializes and writes atomically. <see cref="SaveResult.Unchanged"/> means the
    /// file already matched the settings; only <see cref="SaveResult.Failed"/> is an error.
    /// </summary>
    public SaveResult Save(AppSettings settings)
    {
        settings.Normalize();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        if (string.Equals(json, LastJson, StringComparison.Ordinal))
        {
            Current = settings;
            LastError = null;
            return SaveResult.Unchanged;
        }

        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = ConfigPath + ".tmp";
        // Antivirus scanners and sync clients may briefly lock the file: retry a
        // few times, but never fall back to a non-atomic direct write.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                File.Move(temp, ConfigPath, overwrite: true);
                LastJson = json;
                Current = settings;
                LastError = null;
                return SaveResult.Saved;
            }
            catch (IOException) when (attempt < 3)
            {
                Thread.Sleep(50 * attempt);
            }
            catch (Exception ex)
            {
                LastError = $"无法保存配置文件：{ex.Message}";
                Log.Error($"Failed to write {ConfigPath}", ex);
                try
                {
                    File.Delete(temp);
                }
                catch
                {
                    // Best effort only.
                }

                return SaveResult.Failed;
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
