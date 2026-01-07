using System.CommandLine;
using AetherStitch.Services;
using AetherStitch.Utilities;

namespace AetherStitch.Commands;

/// <summary>
/// Extract 命令 - 从项目中提取字符串到 mapping 文件
/// </summary>
public class ExtractCommand : ICommand
{
    public string Name => "extract";
    public string Description => "Extract strings from C# project to mapping file";

    public Command CreateCommand()
    {
        var command = new Command(Name, Description);

        // 定义选项
        var sourceOption = new Option<string>(
            new[] { "--source", "-s" },
            description: "Source project path (.csproj file or directory)")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            new[] { "--output", "-o" },
            getDefaultValue: () => "localization-mapping.json",
            description: "Output mapping file path");

        var targetOption = new Option<string>(
            "--target",
            getDefaultValue: () => "zh-CN",
            description: "Target language code");

        var excludeOption = new Option<string[]>(
            "--exclude",
            getDefaultValue: () => FileSystemHelper.DefaultExcludePatterns,
            description: "File patterns to exclude");

        var updateOption = new Option<bool>(
            "--update",
            getDefaultValue: () => false,
            description: "Update existing mapping file (preserve translations)");

        var keepDeletedOption = new Option<bool>(
            "--keep-deleted",
            getDefaultValue: () => false,
            description: "Keep deleted strings in mapping file (only with --update)");

        // 添加选项
        command.AddOption(sourceOption);
        command.AddOption(outputOption);
        command.AddOption(targetOption);
        command.AddOption(excludeOption);
        command.AddOption(updateOption);
        command.AddOption(keepDeletedOption);

        // 设置处理程序
        command.SetHandler(async (source, output, target, exclude, update, keepDeleted) =>
        {
            await ExecuteAsync(source, output, target, exclude, update, keepDeleted);
        }, sourceOption, outputOption, targetOption, excludeOption, updateOption, keepDeletedOption);

        return command;
    }

    private async Task<int> ExecuteAsync(
        string sourcePath,
        string outputPath,
        string targetLanguage,
        string[] excludePatterns,
        bool update,
        bool keepDeleted)
    {
        try
        {
            Logger.Info("=== AetherStitch - String Extraction ===");
            Logger.Info($"Source: {sourcePath}");
            Logger.Info($"Output: {outputPath}");
            Logger.Info($"Target Language: {targetLanguage}");
            Logger.Info($"Update Mode: {update}");
            if (update && keepDeleted)
            {
                Logger.Info($"Keep Deleted: {keepDeleted}");
            }
            Logger.Info("");

            // 验证源路径
            if (!FileSystemHelper.ValidatePath(sourcePath, out var errorMessage))
            {
                Logger.Error(errorMessage);
                return 1;
            }

            // 提取字符串
            var extractor = new StringExtractor(excludePatterns);
            var literals = await extractor.ExtractFromProjectAsync(sourcePath);

            if (literals.Count == 0)
            {
                Logger.Warning("No strings found in the project");
                return 0;
            }

            var mappingService = new MappingFileService();
            Models.LocalizationMapping mapping;

            // 检查是否为更新模式
            if (update && File.Exists(outputPath))
            {
                Logger.Info("");
                Logger.Info("Loading existing mapping for update...");

                var existingMapping = await mappingService.LoadMappingAsync(outputPath);
                mapping = mappingService.UpdateMapping(existingMapping, literals, keepDeleted);
            }
            else
            {
                if (update)
                {
                    Logger.Warning($"Update mode specified but mapping file not found: {outputPath}");
                    Logger.Info("Creating new mapping file...");
                }

                // 创建新 Mapping
                var projectName = Path.GetFileNameWithoutExtension(sourcePath);
                if (Directory.Exists(sourcePath))
                {
                    projectName = new DirectoryInfo(sourcePath).Name;
                }

                mapping = mappingService.CreateMapping(literals, projectName, "en-US", targetLanguage);
            }

            // 保存 Mapping
            await mappingService.SaveMappingAsync(mapping, outputPath);

            // 显示统计信息
            Logger.Info("");
            Logger.Info("=== Summary ===");
            Logger.Info($"Unique translations: {mapping.Translations.Count}");
            Logger.Info($"String literals: {mapping.Translations.Count(t => t.Type == Models.StringType.Literal)}");
            Logger.Info($"String interpolations: {mapping.Translations.Count(t => t.Type == Models.StringType.Interpolation)}");
            Logger.Info($"Total contexts (occurrences): {mapping.Metadata.TotalContexts}");
            Logger.Info($"Files processed: {mapping.Metadata.FileStatistics.Count}");
            Logger.Info($"Translated: {mapping.Metadata.TranslatedCount}");
            Logger.Info($"Pending: {mapping.Metadata.PendingCount}");
            Logger.Info("");
            Logger.Success($"Mapping file saved: {Path.GetFullPath(outputPath)}");

            return 0;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Extraction failed");
            return 1;
        }
    }
}
