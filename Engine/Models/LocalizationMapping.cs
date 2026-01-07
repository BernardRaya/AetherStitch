namespace AetherStitch.Models;

/// <summary>
/// 表示本地化 Mapping 文件的根模型
/// </summary>
public class LocalizationMapping
{
    /// <summary>
    /// 项目名称
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 源语言代码（如 "en-US"）
    /// </summary>
    public string SourceLanguage { get; set; } = "en-US";

    /// <summary>
    /// 目标语言代码（如 "zh-CN"）
    /// </summary>
    public string TargetLanguage { get; set; } = "zh-CN";

    /// <summary>
    /// Mapping 版本
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 元数据
    /// </summary>
    public MappingMetadata Metadata { get; set; } = new();

    /// <summary>
    /// 翻译单元列表（翻译池模式）
    /// 每个唯一字符串对应一个翻译，包含所有代码上下文引用
    /// </summary>
    public List<Translation> Translations { get; set; } = new();
}
