namespace AetherStitch.Models;

/// <summary>
/// 表示字符串插值中的占位符信息
/// </summary>
public class PlaceholderInfo
{
    /// <summary>
    /// 占位符索引（0, 1, 2...）
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 原始插值表达式（如 "userName" 或 "DateTime.Now:yyyy-MM-dd"）
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// 占位符标记（如 "{0}", "{1}"）
    /// </summary>
    public string PlaceholderToken { get; set; } = string.Empty;
}
