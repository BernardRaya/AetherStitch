namespace AetherStitch.Models;

/// <summary>
/// 翻译单元 - 每个唯一字符串对应一个翻译
/// </summary>
public class Translation
{
    /// <summary>
    /// 唯一标识符（基于源字符串内容的哈希）
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 源语言文本（原始字符串，对于插值字符串为模板格式）
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 目标语言文本（翻译后的字符串）
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 字符串类型（字面量或插值）
    /// </summary>
    public StringType Type { get; set; }

    /// <summary>
    /// 占位符信息（仅对插值字符串有效）
    /// </summary>
    public List<PlaceholderInfo> Placeholders { get; set; } = new();

    /// <summary>
    /// 翻译状态
    /// </summary>
    public TranslationStatus Status { get; set; } = TranslationStatus.Pending;

    /// <summary>
    /// 首次提取时间
    /// </summary>
    public DateTime ExtractedAt { get; set; }

    /// <summary>
    /// 翻译时间
    /// </summary>
    public DateTime? TranslatedAt { get; set; }

    /// <summary>
    /// 翻译人员备注
    /// </summary>
    public string? TranslatorNote { get; set; }

    /// <summary>
    /// 代码上下文引用列表（该字符串在代码中的所有出现位置）
    /// </summary>
    public List<ContextReference> Contexts { get; set; } = new();

    /// <summary>
    /// 出现次数（在代码中使用了多少次）
    /// </summary>
    public int UsageCount => Contexts.Count;
}
