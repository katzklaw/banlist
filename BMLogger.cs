using BepInEx.Logging;

namespace BanListMod;

public static class BMLogger
{
    public static ManualLogSource Log;

    public static void Init(ManualLogSource logSource)
    {
        Log = logSource;
    }

    public static void LogInfo(string msg) => Log?.LogInfo(msg);
    public static void LogWarn(string msg) => Log?.LogWarning(msg);
    public static void LogError(string msg) => Log?.LogError(msg);

    // Aliases matching the call patterns pulled over from the original mod.
    public static void Info(string msg, string tag = null) => Log?.LogInfo(tag != null ? $"[{tag}] {msg}" : msg);
    public static void Warn(string msg, string tag = null) => Log?.LogWarning(tag != null ? $"[{tag}] {msg}" : msg);
    public static void Error(string msg, string tag = null) => Log?.LogError(tag != null ? $"[{tag}] {msg}" : msg);
    public static void Exception(string msg, System.Exception ex = null) => Log?.LogError(ex != null ? $"{msg}: {ex}" : msg);
}
