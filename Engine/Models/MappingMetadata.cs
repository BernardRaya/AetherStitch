namespace AetherStitch.Models;

/// <summary>
/// Mapping 文件的元数据
/// </summary>
public class MappingMetadata
{
    /// <summary>
    /// 唯一翻译单元总数（去重后的字符串数量）
    /// </summary>
    public int TotalTranslations { get; set; }

    /// <summary>
    /// 已翻译的单元数
    /// </summary>
    public int TranslatedCount { get; set; }

    /// <summary>
    /// 待翻译的单元数
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// 字符串在代码中的总出现次数（包括重复）
    /// </summary>
    public int TotalContexts { get; set; }

    /// <summary>
    /// 按文件统计的字符串出现次数
    /// </summary>
    public Dictionary<string, int> FileStatistics { get; set; } = new();

    /// <summary>
    /// 按翻译状态统计
    /// </summary>
    public Dictionary<string, int> StatusStatistics { get; set; } = new();
}
