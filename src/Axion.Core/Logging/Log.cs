using System;
using System.Collections.Generic;

namespace Axion.Core;

public enum LogLevel { Trace, Info, Warn, Error }

public readonly record struct LogEntry(LogLevel Level, string Message, DateTime When, string? Source);

/// <summary>Engine-wide logger. Editor and runtime both subscribe to OnLog.</summary>
public static class Log
{
    private static readonly List<LogEntry> _history = new(capacity: 256);
    public static IReadOnlyList<LogEntry> History => _history;

    public static event Action<LogEntry>? OnLog;

    public static void Trace(string msg, string? src = null) => Emit(LogLevel.Trace, msg, src);
    public static void Info (string msg, string? src = null) => Emit(LogLevel.Info,  msg, src);
    public static void Warn (string msg, string? src = null) => Emit(LogLevel.Warn,  msg, src);
    public static void Error(string msg, string? src = null) => Emit(LogLevel.Error, msg, src);

    public static void Exception(Exception ex, string? src = null)
        => Emit(LogLevel.Error, $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", src);

    private static void Emit(LogLevel level, string msg, string? src)
    {
        var entry = new LogEntry(level, msg, DateTime.UtcNow, src);
        lock (_history)
        {
            _history.Add(entry);
            if (_history.Count > 4096) _history.RemoveRange(0, 1024);
        }
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = level switch
        {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Info  => ConsoleColor.Gray,
            LogLevel.Warn  => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            _              => ConsoleColor.Gray,
        };
        Console.WriteLine($"[{level,-5}] {(src != null ? src + ": " : "")}{msg}");
        Console.ForegroundColor = prev;
        OnLog?.Invoke(entry);
    }
}
