namespace AetherStitch.Models;

/// <summary>
/// 表示从代码中提取的字符串字面量或插值表达式
/// </summary>
public class StringLiteral
{
    /// <summary>
    /// 唯一标识符（基于文件路径、行号和内容的 SHA256 哈希）
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 原始字符串内容（对于插值字符串，已转换为模板格式）
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// 字符串类型（字面量或插值）
    /// </summary>
    public StringType Type { get; set; }

    /// <summary>
    /// 文件路径（相对于项目根目录）
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 行号
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// 列号
    /// </summary>
    public int ColumnNumber { get; set; }

    /// <summary>
    /// 代码上下文（所在的类.方法）
    /// </summary>
    public string CodeContext { get; set; } = string.Empty;

    /// <summary>
    /// 插值占位符信息（仅对插值字符串有效）
    /// </summary>
    public List<PlaceholderInfo> Placeholders { get; set; } = new();

    /// <summary>
    /// 父节点类型（用于识别特殊场景，如常量声明）
    /// </summary>
    public string? ParentNode { get; set; }
}

/// <summary>
/// 字符串类型枚举
/// </summary>
public enum StringType
{
    /// <summary>
    /// 普通字符串字面量（如 "text"）
    /// </summary>
    Literal,

    /// <summary>
    /// 字符串插值表达式（如 $"text {var}"）
    /// </summary>
    Interpolation
}
