using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LightSwitch.Core.Updates;

/// <summary>Transient update bookkeeping, deliberately kept out of the user-facing config.</summary>
public sealed class UpdateState
{
    [JsonPropertyName("lastCheckUtc")]
    public DateTimeOffset? LastCheckUtc { get; set; }

    [JsonPropertyName("postponedVersion")]
    public string? PostponedVersion { get; set; }

    public UpdateState Clone() => new()
    {
        LastCheckUtc = LastCheckUtc,
        PostponedVersion = PostponedVersion,
    };
}

/// <summary>
/// Persists the last-check timestamp and the version the user asked to be
/// reminded about later. This is best-effort state: losing it only causes an
/// extra request or an extra reminder, never a failed update.
/// </summary>
public sealed class UpdateStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public UpdateStateStore(string? statePath = null)
    {
        StatePath = statePath ?? AppPaths.UpdateStatePath;
    }

    public string StatePath { get; }

    public UpdateState Load()
    {
        if (!File.Exists(StatePath))
        {
            return new UpdateState();
        }

        try
        {
            var json = File.ReadAllText(StatePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<UpdateState>(json, JsonOptions) ?? new UpdateState();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to read update state {StatePath}: {ex.Message}");
            return new UpdateState();
        }
    }

    public bool Save(UpdateState state)
    {
        var directory = Path.GetDirectoryName(StatePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        var temp = StatePath + ".tmp";
        try
        {
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, StatePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to write update state {StatePath}: {ex.Message}");
            try
            {
                File.Delete(temp);
            }
            catch
            {
                // Best effort only.
            }

            return false;
        }
    }
}
