using System.CommandLine;
using AetherStitch.Services;
using AetherStitch.Utilities;

namespace AetherStitch.Commands;

/// <summary>
/// Stats 命令 - 显示 mapping 文件的统计信息
/// </summary>
public class StatsCommand : ICommand
{
    public string Name => "stats";
    public string Description => "Display mapping file statistics";

    public Command CreateCommand()
    {
        var command = new Command(Name, Description);

        // 定义选项
        var mappingOption = new Option<string>(
            new[] { "--mapping", "-m" },
            description: "Mapping file path")
        {
            IsRequired = true
        };

        // 添加选项
        command.AddOption(mappingOption);

        // 设置处理程序
        command.SetHandler(async (mapping) =>
        {
            await ExecuteAsync(mapping);
        }, mappingOption);

        return command;
    }

    private async Task<int> ExecuteAsync(string mappingPath)
    {
        try
        {
            Logger.Info("=== AetherStitch - Mapping Statistics ===");
            Logger.Info("");

            // 加载 Mapping
            var service = new MappingFileService();
            var mapping = await service.LoadMappingAsync(mappingPath);

            // 生成并显示统计报告
            var report = service.GenerateStatisticsReport(mapping);
            Console.WriteLine(report);

            return 0;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Failed to generate statistics");
            return 1;
        }
    }
}
