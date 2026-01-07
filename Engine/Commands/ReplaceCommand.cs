using System.CommandLine;
using AetherStitch.Services;
using AetherStitch.Utilities;

namespace AetherStitch.Commands;

/// <summary>
/// Replace 命令 - 应用翻译到项目代码
/// </summary>
public class ReplaceCommand : ICommand
{
    public string Name => "replace";
    public string Description => "Apply translations to project code";

    public Command CreateCommand()
    {
        var command = new Command(Name, Description);

        // 定义选项
        var sourceOption = new Option<string>(
            new[] { "--source", "-s" },
            description: "Source project path (will be copied)")
        {
            IsRequired = true
        };

        var mappingOption = new Option<string>(
            new[] { "--mapping", "-m" },
            description: "Mapping file path")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            new[] { "--output", "-o" },
            description: "Output project path")
        {
            IsRequired = true
        };

        var validateOption = new Option<bool>(
            "--validate",
            getDefaultValue: () => true,
            description: "Validate mapping before replacement");

        var overwriteOption = new Option<bool>(
            "--overwrite",
            getDefaultValue: () => false,
            description: "Overwrite output directory if exists");

        var dryRunOption = new Option<bool>(
            "--dry-run",
            getDefaultValue: () => false,
            description: "Simulate replacement without modifying files");

        // 添加选项
        command.AddOption(sourceOption);
        command.AddOption(mappingOption);
        command.AddOption(outputOption);
        command.AddOption(validateOption);
        command.AddOption(overwriteOption);
        command.AddOption(dryRunOption);

        // 设置处理程序
        command.SetHandler(async (source, mapping, output, validate, overwrite, dryRun) =>
        {
            await ExecuteAsync(source, mapping, output, validate, overwrite, dryRun);
        }, sourceOption, mappingOption, outputOption, validateOption, overwriteOption, dryRunOption);

        return command;
    }

    private async Task<int> ExecuteAsync(
        string sourcePath,
        string mappingPath,
        string outputPath,
        bool validate,
        bool overwrite,
        bool dryRun)
    {
        try
        {
            Logger.Info("=== AetherStitch - String Replacement ===");
            Logger.Info($"Source: {sourcePath}");
            Logger.Info($"Mapping: {mappingPath}");
            Logger.Info($"Output: {outputPath}");
            Logger.Info($"Dry Run: {dryRun}");
            Logger.Info("");

            // 验证源路径
            if (!FileSystemHelper.ValidatePath(sourcePath, out var sourceError))
            {
                Logger.Error(sourceError);
                return 1;
            }

            // 验证 mapping 文件
            if (!File.Exists(mappingPath))
            {
                Logger.Error($"Mapping file not found: {mappingPath}");
                return 1;
            }

            // 加载 mapping
            var mappingService = new MappingFileService();
            var mapping = await mappingService.LoadMappingAsync(mappingPath);

            // 验证 mapping
            if (validate)
            {
                Logger.Info("Validating mapping...");
                var validationResult = mappingService.ValidateMapping(mapping, strict: false);

                if (!validationResult.IsValid)
                {
                    Logger.Error($"Mapping validation failed with {validationResult.Errors.Count} errors");
                    foreach (var error in validationResult.Errors.Take(10))
                    {
                        Logger.Error($"  - {error}");
                    }
                    return 1;
                }

                Logger.Success("Mapping validation passed");
            }

            // 检查是否有已翻译的内容
            var translatedCount = mapping.Translations.Count(t =>
                !string.IsNullOrWhiteSpace(t.Target) && t.Target != t.Source);

            if (translatedCount == 0)
            {
                Logger.Warning("No translations found in mapping file (all targets are same as source)");
                Logger.Info("Tip: Edit the mapping file and change 'target' fields to apply translations");
                return 1;
            }

            Logger.Info($"Found {translatedCount} translated strings to apply");
            Logger.Info("");

            // 复制项目
            if (!dryRun)
            {
                Logger.Info("Copying project...");
                var copier = new ProjectCopier();
                copier.CopyProject(sourcePath, outputPath, overwrite);
                Logger.Success("Project copied");
                Logger.Info("");
            }

            // 替换字符串
            Logger.Info("Replacing strings...");
            var replacer = new CodeReplacer();
            var targetPath = dryRun ? sourcePath : outputPath;
            var result = await replacer.ReplaceInProjectAsync(targetPath, mapping, dryRun);

            // 显示结果
            Logger.Info("");
            Logger.Info("=== Replacement Summary ===");
            Logger.Info($"Files modified: {result.FilesModified}");
            Logger.Info($"Total replacements: {result.TotalReplacements}");

            if (result.Errors.Count > 0)
            {
                Logger.Warning($"Errors: {result.Errors.Count}");
                foreach (var error in result.Errors.Take(10))
                {
                    Logger.Error($"  - {error}");
                }
            }

            Logger.Info("");

            if (dryRun)
            {
                Logger.Info("Dry run completed - no files were modified");
            }
            else
            {
                Logger.Success($"Localized project created: {Path.GetFullPath(outputPath)}");
            }

            return result.Errors.Count > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Replacement failed");
            return 1;
        }
    }
}
