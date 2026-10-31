using System.Text;

namespace LightSwitch.Core;

/// <summary>
/// Tiny append-only log with a single rotation. Deliberately allocation-light:
/// nothing is buffered, and the file is only opened while writing.
/// </summary>
public static class Log
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();
    private static string? _path;

    public static string? Path => _path;

    public static void Initialize(string? path = null)
    {
        lock (Gate)
        {
            _path = path ?? AppPaths.LogPath;
            var dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                RotateIfNeeded(_path);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(_path, line, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }

    private static void RotateIfNeeded(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < MaxBytes)
            {
                return;
            }

            var old = path + ".old";
            File.Move(path, old, overwrite: true);
        }
        catch
        {
            // If rotation fails we keep appending; a large log is harmless.
        }
    }
}
