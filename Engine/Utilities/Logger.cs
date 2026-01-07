namespace AetherStitch.Utilities;

/// <summary>
/// 日志记录工具类
/// </summary>
public static class Logger
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Success
    }

    private static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// 设置最小日志级别
    /// </summary>
    public static void SetMinimumLevel(LogLevel level)
    {
        MinimumLevel = level;
    }

    /// <summary>
    /// 记录调试信息
    /// </summary>
    public static void Debug(string message)
    {
        Log(LogLevel.Debug, message, ConsoleColor.Gray);
    }

    /// <summary>
    /// 记录普通信息
    /// </summary>
    public static void Info(string message)
    {
        Log(LogLevel.Info, message, ConsoleColor.White);
    }

    /// <summary>
    /// 记录警告信息
    /// </summary>
    public static void Warning(string message)
    {
        Log(LogLevel.Warning, message, ConsoleColor.Yellow);
    }

    /// <summary>
    /// 记录错误信息
    /// </summary>
    public static void Error(string message)
    {
        Log(LogLevel.Error, message, ConsoleColor.Red);
    }

    /// <summary>
    /// 记录成功信息
    /// </summary>
    public static void Success(string message)
    {
        Log(LogLevel.Success, message, ConsoleColor.Green);
    }

    /// <summary>
    /// 记录异常信息
    /// </summary>
    public static void Exception(Exception ex, string? context = null)
    {
        var message = context != null
            ? $"{context}: {ex.Message}"
            : ex.Message;

        Log(LogLevel.Error, message, ConsoleColor.Red);

        if (MinimumLevel == LogLevel.Debug && ex.StackTrace != null)
        {
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void Log(LogLevel level, string message, ConsoleColor color)
    {
        if (level < MinimumLevel) return;

        var prefix = level switch
        {
            LogLevel.Debug => "[DEBUG] ",
            LogLevel.Info => "[INFO]  ",
            LogLevel.Warning => "[WARN]  ",
            LogLevel.Error => "[ERROR] ",
            LogLevel.Success => "[OK]    ",
            _ => ""
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"{prefix}{message}");
        Console.ForegroundColor = originalColor;
    }

    /// <summary>
    /// 显示进度（同一行更新）
    /// </summary>
    public static void Progress(string message)
    {
        Console.Write($"\r{message}");
    }

    /// <summary>
    /// 完成进度显示（换行）
    /// </summary>
    public static void ProgressComplete()
    {
        Console.WriteLine();
    }
}
