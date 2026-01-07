using System.CommandLine;
using AetherStitch.Commands;
using AetherStitch.Utilities;

namespace AetherStitch;

/// <summary>
/// AetherStitch - C# 本地化工具
/// 用于提取、翻译和替换 C# 项目中的字符串
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // 设置日志级别
        Logger.SetMinimumLevel(Logger.LogLevel.Info);

        // 创建根命令
        var rootCommand = new RootCommand("AetherStitch - C# Localization Tool")
        {
            Name = "aetherstitch"
        };

        // 注册所有命令
        var commands = new ICommand[]
        {
            new ExtractCommand(),
            new ValidateCommand(),
            new StatsCommand(),
            new ReplaceCommand()
        };

        foreach (var cmd in commands)
        {
            rootCommand.AddCommand(cmd.CreateCommand());
        }

        // 执行命令
        try
        {
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Unhandled exception");
            return 1;
        }
    }
}
