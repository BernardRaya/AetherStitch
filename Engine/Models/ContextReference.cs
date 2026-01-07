namespace AetherStitch.Models;

/// <summary>
/// 代码上下文引用 - 记录字符串在代码中的使用位置
/// </summary>
public class ContextReference
{
    /// <summary>
    /// 文件路径（相对于项目根目录）
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 行号
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// 代码上下文（类名.方法名）
    /// </summary>
    public string CodeContext { get; set; } = string.Empty;

    /// <summary>
    /// 上下文备注（可选，用于给翻译人员提供额外信息）
    /// </summary>
    public string? Note { get; set; }
}
