using System.CommandLine;
using AetherStitch.Services;
using AetherStitch.Utilities;

namespace AetherStitch.Commands;

/// <summary>
/// Validate 命令 - 验证 mapping 文件
/// </summary>
public class ValidateCommand : ICommand
{
    public string Name => "validate";
    public string Description => "Validate mapping file";

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

        var strictOption = new Option<bool>(
            "--strict",
            getDefaultValue: () => false,
            description: "Strict mode (all entries must be translated)");

        // 添加选项
        command.AddOption(mappingOption);
        command.AddOption(strictOption);

        // 设置处理程序
        command.SetHandler(async (mapping, strict) =>
        {
            await ExecuteAsync(mapping, strict);
        }, mappingOption, strictOption);

        return command;
    }

    private async Task<int> ExecuteAsync(string mappingPath, bool strict)
    {
        try
        {
            Logger.Info("=== AetherStitch - Mapping Validation ===");
            Logger.Info($"Mapping file: {mappingPath}");
            Logger.Info($"Strict mode: {strict}");
            Logger.Info("");

            // 加载 Mapping
            var service = new MappingFileService();
            var mapping = await service.LoadMappingAsync(mappingPath);

            // 验证
            var result = service.ValidateMapping(mapping, strict);

            // 显示结果
            Logger.Info("");
            Logger.Info("=== Validation Results ===");

            if (result.Errors.Count > 0)
            {
                Logger.Error($"Found {result.Errors.Count} error(s):");
                foreach (var error in result.Errors)
                {
                    Logger.Error($"  - {error}");
                }
            }

            if (result.Warnings.Count > 0)
            {
                Logger.Warning($"Found {result.Warnings.Count} warning(s):");
                foreach (var warning in result.Warnings)
                {
                    Logger.Warning($"  - {warning}");
                }
            }

            if (result.IsValid)
            {
                Logger.Success("Validation passed!");
                return 0;
            }
            else
            {
                Logger.Error("Validation failed!");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Validation failed");
            return 1;
        }
    }
}
