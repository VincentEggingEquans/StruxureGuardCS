using System.Text;

namespace StruxureGuard.Core.Logging;

public sealed class FileLogSink
{
    private readonly string _folder;
    private readonly object _lock = new();

    public FileLogSink(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
    }

    public void Write(LogEvent e)
    {
        var path = Path.Combine(_folder, $"struxureguard_{DateTime.Now:yyyy-MM-dd}.log");
        var line = FormatLine(e);

        lock (_lock)
        {
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    private static string FormatLine(LogEvent e)
    {
        var ex = e.Exception is null ? "" : $" | EX: {e.Exception}";
        return $"{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{e.Level}] ({e.Category}) {e.Message}{ex}";
    }
}
