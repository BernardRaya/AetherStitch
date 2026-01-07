using Newtonsoft.Json;
using AetherStitch.Models;
using AetherStitch.Utilities;
using AetherStitch.Analyzers;

namespace AetherStitch.Services;

/// <summary>
/// Mapping 文件管理服务 - 处理 JSON 序列化、验证和统计
/// </summary>
public class MappingFileService
{
    private readonly InterpolationHandler _interpolationHandler;

    public MappingFileService()
    {
        _interpolationHandler = new InterpolationHandler();
    }

    /// <summary>
    /// 从字符串字面量列表创建 Mapping 对象（翻译池模式）
    /// </summary>
    public LocalizationMapping CreateMapping(
        List<StringLiteral> literals,
        string projectName,
        string sourceLanguage = "en-US",
        string targetLanguage = "zh-CN")
    {
        var now = DateTime.UtcNow;

        // 按字符串内容分组（相同内容的字符串合并为一个翻译单元）
        var translationGroups = literals.GroupBy(l => new
        {
            l.OriginalText,
            l.Type,
            PlaceholdersJson = System.Text.Json.JsonSerializer.Serialize(l.Placeholders)
        });

        var translations = new List<Translation>();

        foreach (var group in translationGroups)
        {
            // 为每个唯一字符串生成基于内容的 Key
            var key = GenerateContentBasedKey(group.Key.OriginalText, group.Key.Type);

            // 收集所有代码上下文
            var contexts = group.Select(literal => new ContextReference
            {
                FilePath = literal.FilePath,
                LineNumber = literal.LineNumber,
                CodeContext = literal.CodeContext,
                Note = null
            }).ToList();

            // 创建翻译单元
            var translation = new Translation
            {
                Key = key,
                Source = group.Key.OriginalText,
                Target = group.Key.OriginalText, // 默认值与原文相同
                Type = group.Key.Type,
                Placeholders = group.First().Placeholders,
                Status = TranslationStatus.Pending,
                ExtractedAt = now,
                TranslatedAt = null,
                TranslatorNote = null,
                Contexts = contexts
            };

            translations.Add(translation);
        }

        var mapping = new LocalizationMapping
        {
            ProjectName = projectName,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            Version = "2.0.0", // 新版本号
            CreatedAt = now,
            UpdatedAt = now,
            Translations = translations,
            Metadata = GenerateMetadata(translations)
        };

        return mapping;
    }

    /// <summary>
    /// 生成基于内容的唯一 Key（使用 SHA256 哈希）
    /// </summary>
    private string GenerateContentBasedKey(string content, StringType type)
    {
        var input = $"{type}:{content}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16].ToLower();
    }

    /// <summary>
    /// 保存 Mapping 到 JSON 文件
    /// </summary>
    public async Task SaveMappingAsync(LocalizationMapping mapping, string filePath)
    {
        Logger.Info($"Saving mapping to: {filePath}");

        try
        {
            FileSystemHelper.EnsureDirectoryExists(filePath);

            // 更新时间和元数据
            mapping.UpdatedAt = DateTime.UtcNow;
            mapping.Metadata = GenerateMetadata(mapping.Translations);

            // 序列化为 JSON（使用缩进便于编辑）
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include
            };

            var json = JsonConvert.SerializeObject(mapping, settings);
            await File.WriteAllTextAsync(filePath, json);

            Logger.Success($"Mapping saved successfully: {mapping.Translations.Count} translations ({mapping.Metadata.TotalContexts} contexts)");
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Failed to save mapping");
            throw;
        }
    }

    /// <summary>
    /// 从 JSON 文件加载 Mapping
    /// </summary>
    public async Task<LocalizationMapping> LoadMappingAsync(string filePath)
    {
        Logger.Info($"Loading mapping from: {filePath}");

        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Mapping file not found: {filePath}");
            }

            var json = await File.ReadAllTextAsync(filePath);
            var mapping = JsonConvert.DeserializeObject<LocalizationMapping>(json);

            if (mapping == null)
            {
                throw new InvalidOperationException("Failed to deserialize mapping file");
            }

            Logger.Success($"Mapping loaded: {mapping.Translations.Count} translations ({mapping.Metadata.TotalContexts} contexts)");
            return mapping;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Failed to load mapping");
            throw;
        }
    }

    /// <summary>
    /// 验证 Mapping 文件
    /// </summary>
    public ValidationResult ValidateMapping(LocalizationMapping mapping, bool strict = false)
    {
        Logger.Info("Validating mapping...");

        var result = new ValidationResult();

        foreach (var translation in mapping.Translations)
        {
            // 验证必填字段
            if (string.IsNullOrWhiteSpace(translation.Key))
            {
                result.AddError($"Translation for '{translation.Source}' has empty Key");
            }

            if (string.IsNullOrWhiteSpace(translation.Source))
            {
                result.AddError($"Translation {translation.Key} has empty source text");
            }

            // 严格模式：检查翻译是否完成
            if (strict && (string.IsNullOrWhiteSpace(translation.Target) || translation.Target == translation.Source))
            {
                result.AddError($"Translation {translation.Key} is not translated (strict mode)");
            }

            // 验证占位符
            if (translation.Type == StringType.Interpolation && translation.Placeholders.Count > 0)
            {
                var placeholderValidation = ValidatePlaceholders(translation);
                if (!placeholderValidation.IsValid)
                {
                    foreach (var error in placeholderValidation.Errors)
                    {
                        result.AddError($"Translation {translation.Key}: {error}");
                    }
                }
            }

            // 验证上下文
            if (translation.Contexts == null || translation.Contexts.Count == 0)
            {
                result.AddWarning($"Translation {translation.Key} has no contexts");
            }
            else
            {
                foreach (var context in translation.Contexts)
                {
                    // 检查文件路径格式
                    if (context.FilePath.Contains('\\'))
                    {
                        result.AddWarning($"Translation {translation.Key} at {context.FilePath} has Windows-style path separators");
                    }
                }
            }
        }

        // 检查重复 Key
        var duplicateKeys = mapping.Translations
            .GroupBy(t => t.Key)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var key in duplicateKeys)
        {
            result.AddError($"Duplicate translation key: {key}");
        }

        if (result.IsValid)
        {
            Logger.Success($"Validation passed: {mapping.Translations.Count} translations");
        }
        else
        {
            Logger.Error($"Validation failed: {result.Errors.Count} errors, {result.Warnings.Count} warnings");
        }

        return result;
    }

    /// <summary>
    /// 验证单个条目的占位符
    /// </summary>
    private ValidationResult ValidatePlaceholders(Translation translation)
    {
        var result = new ValidationResult();

        // 如果没有翻译文本，跳过验证
        if (string.IsNullOrWhiteSpace(translation.Target))
        {
            return result;
        }

        // 验证原文占位符
        if (!_interpolationHandler.ValidatePlaceholders(translation.Source, translation.Placeholders, out var originalError))
        {
            result.AddError($"Original text: {originalError}");
        }

        // 验证译文占位符数量和索引
        var translatedPlaceholderCount = CountPlaceholders(translation.Target);
        if (translatedPlaceholderCount != translation.Placeholders.Count)
        {
            result.AddError($"Placeholder count mismatch: original has {translation.Placeholders.Count}, translation has {translatedPlaceholderCount}");
        }

        // 验证译文中的占位符索引
        for (var i = 0; i < translation.Placeholders.Count; i++)
        {
            var token = $"{{{i}}}";
            if (!translation.Target.Contains(token))
            {
                result.AddWarning($"Missing placeholder {token} in translated text");
            }
        }

        return result;
    }

    /// <summary>
    /// 计算字符串中的占位符数量
    /// </summary>
    private int CountPlaceholders(string text)
    {
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{' && i + 1 < text.Length && text[i + 1] != '{')
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 生成统计元数据（新版本 - 翻译池模式）
    /// </summary>
    private MappingMetadata GenerateMetadata(List<Translation> translations)
    {
        var metadata = new MappingMetadata
        {
            TotalTranslations = translations.Count,
            TranslatedCount = translations.Count(t => IsTranslated(t)),
            PendingCount = translations.Count(t => !IsTranslated(t)),
            TotalContexts = translations.Sum(t => t.Contexts.Count),
            FileStatistics = translations
                .SelectMany(t => t.Contexts)
                .GroupBy(c => c.FilePath)
                .ToDictionary(g => g.Key, g => g.Count()),
            StatusStatistics = translations
                .GroupBy(t => t.Status.ToString())
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return metadata;
    }

    /// <summary>
    /// 生成统计元数据（旧版本 - 传统模式，向后兼容）
    /// </summary>
    private MappingMetadata GenerateMetadata(List<TranslationEntry> entries)
    {
        var metadata = new MappingMetadata
        {
            TotalTranslations = entries.Count,
            TranslatedCount = entries.Count(e => IsTranslated(e)),
            PendingCount = entries.Count(e => !IsTranslated(e)),
            TotalContexts = entries.Count,
            FileStatistics = entries
                .GroupBy(e => e.FilePath)
                .ToDictionary(g => g.Key, g => g.Count()),
            StatusStatistics = entries
                .GroupBy(e => e.Status.ToString())
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return metadata;
    }

    /// <summary>
    /// 判断翻译单元是否已翻译（target 与 source 不同）
    /// </summary>
    private bool IsTranslated(Translation translation)
    {
        return !string.IsNullOrWhiteSpace(translation.Target)
               && translation.Target != translation.Source;
    }

    /// <summary>
    /// 判断条目是否已翻译（translatedText 与 originalText 不同）
    /// </summary>
    private bool IsTranslated(TranslationEntry entry)
    {
        return !string.IsNullOrWhiteSpace(entry.TranslatedText)
               && entry.TranslatedText != entry.OriginalText;
    }

    /// <summary>
    /// 增量更新 Mapping - 保留已翻译内容，更新变化的部分（翻译池模式）
    /// </summary>
    /// <param name="existingMapping">现有的 mapping 文件</param>
    /// <param name="newLiterals">新提取的字符串列表</param>
    /// <param name="keepDeleted">是否保留已删除的条目</param>
    /// <returns>更新后的 mapping</returns>
    public LocalizationMapping UpdateMapping(
        LocalizationMapping existingMapping,
        List<StringLiteral> newLiterals,
        bool keepDeleted = false)
    {
        Logger.Info("Performing incremental update (Translation Pool mode)...");

        var now = DateTime.UtcNow;
        var updatedTranslations = new List<Translation>();

        // 创建现有翻译的字典（按Key索引），处理可能的重复Key
        var existingTranslationsDict = existingMapping.Translations
            .GroupBy(t => t.Key)
            .Select(g =>
            {
                if (g.Count() > 1)
                {
                    Logger.Warning($"Found duplicate key '{g.Key}' in existing mapping, keeping the latest one");
                    // 保留最新翻译的（根据TranslatedAt或ExtractedAt）
                    return g.OrderByDescending(t => t.TranslatedAt ?? t.ExtractedAt).First();
                }
                return g.First();
            })
            .ToDictionary(t => t.Key, t => t);

        // 按字符串内容分组新提取的字面量
        var literalsByContent = newLiterals
            .GroupBy(l => new { l.OriginalText, l.Type })
            .ToList();

        var stats = new
        {
            Unchanged = 0,
            Updated = 0,
            Added = 0,
            Deleted = 0
        };

        // 处理每个唯一字符串
        foreach (var group in literalsByContent)
        {
            var key = GenerateContentBasedKey(group.Key.OriginalText, group.Key.Type);
            var contexts = group.Select(l => new ContextReference
            {
                FilePath = l.FilePath,
                LineNumber = l.LineNumber,
                CodeContext = l.CodeContext
            }).ToList();

            if (existingTranslationsDict.TryGetValue(key, out var existingTranslation))
            {
                // 检查源文本是否变化
                if (existingTranslation.Source == group.Key.OriginalText)
                {
                    // 内容未变化，保留翻译并更新上下文
                    var updatedTranslation = new Translation
                    {
                        Key = existingTranslation.Key,
                        Source = existingTranslation.Source,
                        Target = existingTranslation.Target,
                        Type = existingTranslation.Type,
                        Status = existingTranslation.Status,
                        Contexts = contexts,
                        ExtractedAt = existingTranslation.ExtractedAt,
                        TranslatedAt = existingTranslation.TranslatedAt,
                        TranslatorNote = existingTranslation.TranslatorNote
                    };
                    updatedTranslations.Add(updatedTranslation);
                    stats = stats with { Unchanged = stats.Unchanged + 1 };
                }
                else
                {
                    // 源文本变化了，重置翻译
                    var updatedTranslation = new Translation
                    {
                        Key = key, // 新的hash
                        Source = group.Key.OriginalText,
                        Target = group.Key.OriginalText, // 重置为源文本
                        Type = group.Key.Type,
                        Status = TranslationStatus.Pending,
                        Contexts = contexts,
                        ExtractedAt = now,
                        TranslatedAt = null,
                        TranslatorNote = $"[Updated] Previous: \"{existingTranslation.Source}\" -> \"{existingTranslation.Target}\""
                    };
                    updatedTranslations.Add(updatedTranslation);
                    stats = stats with { Updated = stats.Updated + 1 };
                    Logger.Warning($"String updated: \"{existingTranslation.Source}\" -> \"{group.Key.OriginalText}\"");
                }

                // 从字典中移除，剩余的就是已删除的
                existingTranslationsDict.Remove(key);
            }
            else
            {
                // 新增的字符串
                var newTranslation = new Translation
                {
                    Key = key,
                    Source = group.Key.OriginalText,
                    Target = group.Key.OriginalText,
                    Type = group.Key.Type,
                    Status = TranslationStatus.Pending,
                    Contexts = contexts,
                    ExtractedAt = now,
                    TranslatedAt = null,
                    TranslatorNote = "[New]"
                };
                updatedTranslations.Add(newTranslation);
                stats = stats with { Added = stats.Added + 1 };
                Logger.Info($"New string found: \"{group.Key.OriginalText}\" ({contexts.Count} contexts)");
            }
        }

        // 处理已删除的翻译项
        if (keepDeleted && existingTranslationsDict.Count > 0)
        {
            foreach (var deletedTranslation in existingTranslationsDict.Values)
            {
                deletedTranslation.Status = TranslationStatus.Ignored;
                deletedTranslation.TranslatorNote = (deletedTranslation.TranslatorNote ?? "") + " [Deleted from source]";
                deletedTranslation.Contexts = new List<ContextReference>(); // 清空上下文
                updatedTranslations.Add(deletedTranslation);
            }
            stats = stats with { Deleted = existingTranslationsDict.Count };
            Logger.Warning($"{existingTranslationsDict.Count} translations deleted from source (kept in mapping)");
        }
        else if (existingTranslationsDict.Count > 0)
        {
            stats = stats with { Deleted = existingTranslationsDict.Count };
            Logger.Warning($"{existingTranslationsDict.Count} translations deleted from source (removed from mapping)");
        }

        // 创建更新后的 mapping
        var updatedMapping = new LocalizationMapping
        {
            ProjectName = existingMapping.ProjectName,
            SourceLanguage = existingMapping.SourceLanguage,
            TargetLanguage = existingMapping.TargetLanguage,
            Version = existingMapping.Version,
            CreatedAt = existingMapping.CreatedAt,
            UpdatedAt = now,
            Translations = updatedTranslations,
            Metadata = new MappingMetadata
            {
                TotalTranslations = updatedTranslations.Count,
                TranslatedCount = updatedTranslations.Count(t => t.Status == TranslationStatus.Translated),
                PendingCount = updatedTranslations.Count(t => t.Status == TranslationStatus.Pending),
                TotalContexts = updatedTranslations.Sum(t => t.Contexts.Count)
            }
        };

        // 显示更新统计
        Logger.Info("");
        Logger.Info("=== Update Summary (Translation Pool) ===");
        Logger.Success($"Unchanged: {stats.Unchanged}");
        Logger.Info($"Updated: {stats.Updated}");
        Logger.Info($"Added: {stats.Added}");
        Logger.Warning($"Deleted: {stats.Deleted}");
        Logger.Info($"Total contexts: {updatedMapping.Metadata.TotalContexts}");

        return updatedMapping;
    }

    /// <summary>
    /// 比较两个占位符列表是否相等
    /// </summary>
    private bool ArePlaceholdersEqual(List<PlaceholderInfo> list1, List<PlaceholderInfo> list2)
    {
        if (list1.Count != list2.Count) return false;

        for (var i = 0; i < list1.Count; i++)
        {
            if (list1[i].Index != list2[i].Index ||
                list1[i].Expression != list2[i].Expression ||
                list1[i].PlaceholderToken != list2[i].PlaceholderToken)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 生成统计报告
    /// </summary>
    public string GenerateStatisticsReport(LocalizationMapping mapping)
    {
        var report = new System.Text.StringBuilder();

        report.AppendLine("=== Localization Mapping Statistics ===");
        report.AppendLine($"Project: {mapping.ProjectName}");
        report.AppendLine($"Source Language: {mapping.SourceLanguage}");
        report.AppendLine($"Target Language: {mapping.TargetLanguage}");
        report.AppendLine($"Version: {mapping.Version}");
        report.AppendLine();

        report.AppendLine("Overall Progress:");
        report.AppendLine($"  Unique Translations: {mapping.Metadata.TotalTranslations}");
        report.AppendLine($"  Translated: {mapping.Metadata.TranslatedCount} ({GetPercentage(mapping.Metadata.TranslatedCount, mapping.Metadata.TotalTranslations)}%)");
        report.AppendLine($"  Pending: {mapping.Metadata.PendingCount} ({GetPercentage(mapping.Metadata.PendingCount, mapping.Metadata.TotalTranslations)}%)");
        report.AppendLine($"  Total Contexts: {mapping.Metadata.TotalContexts}");
        report.AppendLine();

        report.AppendLine("By File (context occurrences):");
        foreach (var (file, count) in mapping.Metadata.FileStatistics.OrderByDescending(x => x.Value))
        {
            // 统计该文件中已翻译的字符串数（基于上下文）
            var translatedInFile = mapping.Translations
                .Where(t => IsTranslated(t) && t.Contexts.Any(c => c.FilePath == file))
                .SelectMany(t => t.Contexts.Where(c => c.FilePath == file))
                .Count();
            report.AppendLine($"  {file}: {count} occurrences ({translatedInFile} translated)");
        }

        report.AppendLine();
        report.AppendLine("By Status:");
        foreach (var (status, count) in mapping.Metadata.StatusStatistics.OrderByDescending(x => x.Value))
        {
            report.AppendLine($"  {status}: {count}");
        }

        return report.ToString();
    }

    private double GetPercentage(int value, int total)
    {
        return total > 0 ? Math.Round((double)value / total * 100, 1) : 0;
    }
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public bool IsValid => Errors.Count == 0;

    public void AddError(string message) => Errors.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
}
