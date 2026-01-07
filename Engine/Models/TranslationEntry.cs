namespace AetherStitch.Models;

/// <summary>
/// 表示 Mapping 文件中的单个翻译条目
/// </summary>
public class TranslationEntry
{
    /// <summary>
    /// 唯一标识符（对应 StringLiteral 的 Id）
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 原始文本（对于插值字符串为模板格式）
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// 翻译后的文本（待填充）
    /// </summary>
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>
    /// 字符串类型
    /// </summary>
    public StringType Type { get; set; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 行号
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// 代码上下文
    /// </summary>
    public string CodeContext { get; set; } = string.Empty;

    /// <summary>
    /// 占位符信息（仅对插值字符串有效）
    /// </summary>
    public List<PlaceholderInfo> Placeholders { get; set; } = new();

    /// <summary>
    /// 翻译状态
    /// </summary>
    public TranslationStatus Status { get; set; } = TranslationStatus.Pending;

    /// <summary>
    /// 提取时间
    /// </summary>
    public DateTime ExtractedAt { get; set; }

    /// <summary>
    /// 翻译时间
    /// </summary>
    public DateTime? TranslatedAt { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// 翻译状态枚举
/// </summary>
public enum TranslationStatus
{
    /// <summary>
    /// 待翻译
    /// </summary>
    Pending,

    /// <summary>
    /// 已翻译
    /// </summary>
    Translated,

    /// <summary>
    /// 已审核
    /// </summary>
    Reviewed,

    /// <summary>
    /// 忽略（如 debug 字符串）
    /// </summary>
    Ignored
}
