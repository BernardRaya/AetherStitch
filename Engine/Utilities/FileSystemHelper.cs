namespace AetherStitch.Utilities;

/// <summary>
/// 文件系统操作辅助类
/// </summary>
public static class FileSystemHelper
{
    /// <summary>
    /// 默认排除的目录模式
    /// </summary>
    public static readonly string[] DefaultExcludePatterns = new[]
    {
        "**/obj/**",
        "**/bin/**",
        "**/.vs/**",
        "**/.git/**",
        "**/node_modules/**"
    };

    /// <summary>
    /// 递归获取目录中的所有 C# 文件
    /// </summary>
    /// <param name="directory">目录路径</param>
    /// <param name="excludePatterns">排除的文件模式</param>
    /// <returns>C# 文件路径列表</returns>
    public static List<string> GetCSharpFiles(string directory, string[]? excludePatterns = null)
    {
        var patterns = excludePatterns ?? DefaultExcludePatterns;
        var files = new List<string>();

        try
        {
            var allFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                if (!ShouldExclude(file, directory, patterns))
                {
                    files.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error scanning directory {directory}: {ex.Message}");
        }

        return files;
    }

    /// <summary>
    /// 判断文件是否应该被排除
    /// </summary>
    private static bool ShouldExclude(string filePath, string basePath, string[] patterns)
    {
        var relativePath = Path.GetRelativePath(basePath, filePath).Replace('\\', '/');

        foreach (var pattern in patterns)
        {
            if (MatchesPattern(relativePath, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 简单的 glob 模式匹配
    /// </summary>
    private static bool MatchesPattern(string path, string pattern)
    {
        // 移除 **/ 前缀和后缀
        pattern = pattern.Replace("**/", "").Replace("/**", "");

        // 简单匹配：检查路径是否包含模式
        if (pattern.Contains('*'))
        {
            var parts = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
            return parts.All(part => path.Contains(part, StringComparison.OrdinalIgnoreCase));
        }

        return path.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取相对路径
    /// </summary>
    public static string GetRelativePath(string basePath, string fullPath)
    {
        return Path.GetRelativePath(basePath, fullPath).Replace('\\', '/');
    }

    /// <summary>
    /// 确保目录存在
    /// </summary>
    public static void EnsureDirectoryExists(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// 递归复制目录
    /// </summary>
    /// <param name="sourceDir">源目录</param>
    /// <param name="targetDir">目标目录</param>
    /// <param name="excludePatterns">排除的文件模式</param>
    public static void CopyDirectory(string sourceDir, string targetDir, string[]? excludePatterns = null)
    {
        var patterns = excludePatterns ?? DefaultExcludePatterns;

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // 复制所有文件
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            if (!ShouldExclude(file, sourceDir, patterns))
            {
                var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }
        }

        // 递归复制子目录
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(directory);

            // 检查目录本身是否应该被排除
            if (ShouldExclude(directory, sourceDir, patterns))
            {
                continue;
            }

            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectory(directory, targetSubDir, patterns);
        }
    }

    /// <summary>
    /// 查找项目文件（.csproj）
    /// </summary>
    public static string? FindProjectFile(string directory)
    {
        var projectFiles = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly);
        return projectFiles.Length > 0 ? projectFiles[0] : null;
    }

    /// <summary>
    /// 验证路径是否存在
    /// </summary>
    public static bool ValidatePath(string path, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "Path cannot be empty";
            return false;
        }

        if (!Path.Exists(path))
        {
            errorMessage = $"Path does not exist: {path}";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
