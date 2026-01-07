using System.CommandLine;

namespace AetherStitch.Commands;

/// <summary>
/// 命令接口
/// </summary>
public interface ICommand
{
    /// <summary>
    /// 命令名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 命令描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 创建命令
    /// </summary>
    Command CreateCommand();
}
