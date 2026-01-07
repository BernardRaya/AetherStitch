using AetherStitch.Utilities;

namespace AetherStitch.Services;

/// <summary>
/// 项目复制服务 - 将项目复制到新目录
/// </summary>
public class ProjectCopier
{
    private readonly string[] _excludePatterns;

    public ProjectCopier(string[]? excludePatterns = null)
    {
        _excludePatterns = excludePatterns ?? FileSystemHelper.DefaultExcludePatterns;
    }

    /// <summary>
    /// 复制项目到目标目录
    /// </summary>
    /// <param name="sourcePath">源项目路径</param>
    /// <param name="targetPath">目标路径</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    public void CopyProject(string sourcePath, string targetPath, bool overwrite = false)
    {
        // 转换为绝对路径
        sourcePath = Path.GetFullPath(sourcePath);
        targetPath = Path.GetFullPath(targetPath);

        Logger.Info($"Copying project from {sourcePath} to {targetPath}");

        // 检查源路径
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
        }

        // 检查目标路径是否在源路径内
        if (IsSubdirectoryOf(targetPath, sourcePath))
        {
            throw new InvalidOperationException($"Target directory cannot be inside source directory. Target: {targetPath}, Source: {sourcePath}");
        }

        // 检查目标路径
        if (Directory.Exists(targetPath))
        {
            if (!overwrite)
            {
                throw new InvalidOperationException($"Target directory already exists: {targetPath}. Use --overwrite to replace it.");
            }

            Logger.Warning($"Target directory exists, will overwrite: {targetPath}");
            Directory.Delete(targetPath, recursive: true);
        }

        // 创建目标目录
        Directory.CreateDirectory(targetPath);

        // 复制文件和目录
        CopyDirectoryRecursive(sourcePath, targetPath);

        Logger.Success($"Project copied successfully");
    }

    /// <summary>
    /// 检查目录是否是另一个目录的子目录
    /// </summary>
    private bool IsSubdirectoryOf(string candidatePath, string parentPath)
    {
        var candidateInfo = new DirectoryInfo(candidatePath);
        var parentInfo = new DirectoryInfo(parentPath);

        var candidate = candidateInfo.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = parentInfo.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               candidate.Equals(parent, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 递归复制目录
    /// </summary>
    private void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        var dirInfo = new DirectoryInfo(sourceDir);

        // 复制所有文件
        foreach (var file in dirInfo.GetFiles())
        {
            var targetFilePath = Path.Combine(targetDir, file.Name);
            file.CopyTo(targetFilePath, overwrite: true);
        }

        // 递归复制子目录
        foreach (var subDir in dirInfo.GetDirectories())
        {
            // 检查是否应该排除该目录
            if (ShouldExcludeDirectory(subDir.Name))
            {
                Logger.Debug($"Skipping directory: {subDir.Name}");
                continue;
            }

            var targetSubDir = Path.Combine(targetDir, subDir.Name);
            Directory.CreateDirectory(targetSubDir);
            CopyDirectoryRecursive(subDir.FullName, targetSubDir);
        }
    }

    /// <summary>
    /// 判断目录是否应该被排除
    /// </summary>
    private bool ShouldExcludeDirectory(string directoryName)
    {
        var excludeDirs = new[] { "bin", "obj", ".vs", ".git", "node_modules", ".idea" };
        return excludeDirs.Any(dir => directoryName.Equals(dir, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取项目中的所有 C# 文件
    /// </summary>
    public List<string> GetCSharpFiles(string projectPath)
    {
        return FileSystemHelper.GetCSharpFiles(projectPath, _excludePatterns);
    }
}
