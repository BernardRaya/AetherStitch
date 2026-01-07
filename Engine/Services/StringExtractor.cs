using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using AetherStitch.Models;
using AetherStitch.Analyzers;
using AetherStitch.Utilities;

namespace AetherStitch.Services;

/// <summary>
/// 字符串提取服务 - 从 C# 项目中提取所有字符串字面量和插值表达式
/// </summary>
public class StringExtractor
{
    private readonly string[] _excludePatterns;

    public StringExtractor(string[]? excludePatterns = null)
    {
        _excludePatterns = excludePatterns ?? FileSystemHelper.DefaultExcludePatterns;
    }

    /// <summary>
    /// 从项目文件提取字符串
    /// </summary>
    /// <param name="projectPath">项目文件路径或项目目录路径</param>
    /// <returns>提取的字符串列表</returns>
    public async Task<List<StringLiteral>> ExtractFromProjectAsync(string projectPath)
    {
        Logger.Info($"Starting string extraction from: {projectPath}");

        // 检查路径是否存在
        if (!Path.Exists(projectPath))
        {
            throw new FileNotFoundException($"Project path not found: {projectPath}");
        }

        // 如果是目录，查找项目文件
        if (Directory.Exists(projectPath))
        {
            var projectFile = FileSystemHelper.FindProjectFile(projectPath);
            if (projectFile == null)
            {
                // 如果没有项目文件，直接扫描 .cs 文件
                Logger.Warning("No .csproj file found, scanning .cs files directly");
                return await ExtractFromDirectoryAsync(projectPath);
            }
            projectPath = projectFile;
        }

        Logger.Info($"Loading project: {projectPath}");

        try
        {
            // 使用 MSBuildWorkspace 加载项目
            using var workspace = MSBuildWorkspace.Create();

            // 订阅工作区诊断
            workspace.WorkspaceFailed += (sender, e) =>
            {
                Logger.Warning($"Workspace diagnostic: {e.Diagnostic.Message}");
            };

            var project = await workspace.OpenProjectAsync(projectPath);
            Logger.Info($"Project loaded: {project.Name}");

            return await ExtractFromProjectAsync(project);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load project with MSBuild: {ex.Message}");
            Logger.Warning("Falling back to directory scanning");

            var projectDir = Path.GetDirectoryName(projectPath) ?? projectPath;
            return await ExtractFromDirectoryAsync(projectDir);
        }
    }

    /// <summary>
    /// 从 Roslyn Project 对象提取字符串
    /// </summary>
    private async Task<List<StringLiteral>> ExtractFromProjectAsync(Project project)
    {
        var allLiterals = new List<StringLiteral>();
        var processedFiles = 0;

        Logger.Info($"Analyzing {project.Documents.Count()} documents...");

        foreach (var document in project.Documents)
        {
            try
            {
                var literals = await ExtractFromDocumentAsync(document, project.FilePath ?? string.Empty);
                allLiterals.AddRange(literals);

                processedFiles++;
                Logger.Progress($"Progress: {processedFiles}/{project.Documents.Count()} files processed, {allLiterals.Count} strings found");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing {document.Name}: {ex.Message}");
            }
        }

        Logger.ProgressComplete();
        Logger.Success($"Extraction complete: {allLiterals.Count} strings found in {processedFiles} files");

        return MergeDuplicates(allLiterals);
    }

    /// <summary>
    /// 从单个文档提取字符串
    /// </summary>
    private async Task<List<StringLiteral>> ExtractFromDocumentAsync(Document document, string projectBasePath)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync();
        var semanticModel = await document.GetSemanticModelAsync();

        if (syntaxTree == null || semanticModel == null)
        {
            return new List<StringLiteral>();
        }

        var root = await syntaxTree.GetRootAsync();
        var basePath = Path.GetDirectoryName(projectBasePath) ?? projectBasePath;

        var walker = new StringLiteralWalker(semanticModel, document.FilePath ?? string.Empty, basePath);
        walker.Visit(root);

        return walker.GetLiterals();
    }

    /// <summary>
    /// 从目录直接扫描 .cs 文件（不使用 MSBuild）
    /// </summary>
    private async Task<List<StringLiteral>> ExtractFromDirectoryAsync(string directory)
    {
        var allLiterals = new List<StringLiteral>();
        var csFiles = FileSystemHelper.GetCSharpFiles(directory, _excludePatterns);

        Logger.Info($"Found {csFiles.Count} C# files to process");

        var processedFiles = 0;

        foreach (var file in csFiles)
        {
            try
            {
                var literals = await ExtractFromFileAsync(file, directory);
                allLiterals.AddRange(literals);

                processedFiles++;
                Logger.Progress($"Progress: {processedFiles}/{csFiles.Count} files processed, {allLiterals.Count} strings found");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing {file}: {ex.Message}");
            }
        }

        Logger.ProgressComplete();
        Logger.Success($"Extraction complete: {allLiterals.Count} strings found in {processedFiles} files");

        return MergeDuplicates(allLiterals);
    }

    /// <summary>
    /// 从单个文件提取字符串（不使用 MSBuild）
    /// </summary>
    private async Task<List<StringLiteral>> ExtractFromFileAsync(string filePath, string basePath)
    {
        var code = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath);
        var compilation = CSharpCompilation.Create("TempCompilation")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = await syntaxTree.GetRootAsync();

        var walker = new StringLiteralWalker(semanticModel, filePath, basePath);
        walker.Visit(root);

        return walker.GetLiterals();
    }

    /// <summary>
    /// 合并重复的字符串（相同 ID）
    /// </summary>
    private List<StringLiteral> MergeDuplicates(List<StringLiteral> literals)
    {
        // 按 ID 分组，保留所有出现位置
        var grouped = literals.GroupBy(l => l.Id).ToList();

        if (grouped.Count < literals.Count)
        {
            var duplicateCount = literals.Count - grouped.Count;
            Logger.Info($"Found {duplicateCount} duplicate strings (same content at different locations)");
        }

        // 对于重复的字符串，我们保留第一个出现的位置
        // 在未来可以扩展为保留所有位置
        return grouped.Select(g => g.First()).ToList();
    }
}
