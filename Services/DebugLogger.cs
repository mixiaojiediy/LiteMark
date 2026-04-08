using System.Text;

namespace LiteMarkWin.Services;

internal static class DebugLogger
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LiteMark");
    private static readonly string LogPath = Path.Combine(LogDirectory, "debug.log");

    public static string CurrentLogPath => LogPath;

    public static void Log(string message)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(
                    LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // 调试日志不能影响主功能
        }
    }

    public static void Reset()
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(LogDirectory);
                File.WriteAllText(
                    LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} log reset{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // 调试日志不能影响主功能
        }
    }
}
