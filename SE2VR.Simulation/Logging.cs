using System.Diagnostics;

namespace SE2VR.Simulation;

 /// <summary>
 /// A basic logging implementation that is easy to find in the SE log files.
 /// </summary>
public static class Logging
{
#pragma warning disable KN023 // HUSH. This IS a singleton pattern.
    private static int _indent;
#pragma warning restore KN023

    public static void IncreaseIndent() => _indent++;

    public static void DecreaseIndent() => _indent = Math.Min(0, _indent - 1);

    public static void ResetIndent() => _indent = 0;

    public static void Exception(Exception ex)
    {
        var frame = new StackTrace(ex, true).GetFrame(0)?.GetMethod();
        string callingClass = frame?.DeclaringType?.Name ?? "Unknown";
        string methodName = frame?.Name ?? "Unknown";

        Log(LogLevel.Exception, "{0}.{1} threw:\n{2}", callingClass, methodName, ex);
    }

    public static void Error(string message, params object[] args)
    {
        Log(LogLevel.Error, message, args);
    }

    public static void Warn(string message, params object[] args)
    {
        Log(LogLevel.Warning, message, args);
    }

    public static void Info(string message, params object[] args)
    {
        Log(LogLevel.Info, message, args);
    }

    public static void Debug(string message, params object[] args)
    {
#if DEBUG
        var frame = new StackTrace().GetFrame(1)?.GetMethod();
        string callingClass = frame?.DeclaringType?.Name ?? "Unknown";
        string methodName = frame?.Name ?? "Unknown";
        Log(LogLevel.Debug, "{0}.{1}: " + message, callingClass, methodName, args);
#endif
    }

    public static void Log(LogLevel level, string message, params object[] args)
    {
        string formattedMessage = string.Format(message, args);
        string indent = new string('|', _indent).Replace("|", "|    ");
        string prefix = $"{nameof(SE2VR)} -> {indent}[{level}]: ";

        foreach (var line in formattedMessage.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            Keen.VRage.Library.Diagnostics.Log.Default.WriteLine($"{prefix}{line}");
    }

    /// <summary>
    /// Level of importance for a logging message.
    /// </summary>
    public enum LogLevel
    {
        Exception,
        Error,
        Warning,
        Info,

        /// <summary>
        /// Debug messages only shown when in debug build
        /// </summary>
        Debug,
    }

}
