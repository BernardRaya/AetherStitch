using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AetherStitch.Models;
using AetherStitch.Analyzers;
using AetherStitch.Utilities;

namespace AetherStitch.Services;

/// <summary>
/// 代码替换服务 - 将翻译应用到代码中
/// </summary>
public class CodeReplacer
{
    private readonly InterpolationHandler _interpolationHandler;

    public CodeReplacer()
    {
        _interpolationHandler = new InterpolationHandler();
    }

    /// <summary>
    /// 替换整个项目中的字符串
    /// </summary>
    public async Task<ReplacementResult> ReplaceInProjectAsync(
        string projectPath,
        LocalizationMapping mapping,
        bool dryRun = false)
    {
        Logger.Info($"Starting string replacement in project: {projectPath}");
        Logger.Info($"Dry run: {dryRun}");

        var result = new ReplacementResult();
        var csFiles = FileSystemHelper.GetCSharpFiles(projectPath);

        Logger.Info($"Found {csFiles.Count} C# files to process");

        foreach (var filePath in csFiles)
        {
            try
            {
                var fileResult = await ReplaceInFileAsync(filePath, mapping, projectPath, dryRun);
                result.Merge(fileResult);

                if (fileResult.ReplacementCount > 0)
                {
                    Logger.Info($"  {Path.GetFileName(filePath)}: {fileResult.ReplacementCount} replacements");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing {filePath}: {ex.Message}");
                result.Errors.Add($"{filePath}: {ex.Message}");
            }
        }

        Logger.Success($"Replacement complete: {result.TotalReplacements} replacements in {result.FilesModified} files");

        return result;
    }

    /// <summary>
    /// 替换单个文件中的字符串
    /// </summary>
    private async Task<ReplacementResult> ReplaceInFileAsync(
        string filePath,
        LocalizationMapping mapping,
        string projectBasePath,
        bool dryRun)
    {
        var result = new ReplacementResult();
        var relativePath = FileSystemHelper.GetRelativePath(projectBasePath, filePath);

        // 查找该文件相关的翻译
        var translationsForFile = mapping.Translations
            .Where(t => t.Contexts.Any(c => c.FilePath == relativePath))
            .ToList();

        if (translationsForFile.Count == 0)
        {
            return result; // 该文件没有需要替换的字符串
        }

        // 读取并解析文件
        var code = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath);
        var root = await syntaxTree.GetRootAsync();

        // 创建替换映射（行号 -> 翻译）
        var replacementMap = BuildReplacementMap(translationsForFile, relativePath);

        // 使用 SyntaxRewriter 替换字符串
        var rewriter = new StringReplacementRewriter(replacementMap, _interpolationHandler, result);
        var newRoot = rewriter.Visit(root);

        // 如果有变化，保存文件
        if (!newRoot.IsEquivalentTo(root))
        {
            result.FilesModified++;

            if (!dryRun)
            {
                var newCode = newRoot.ToFullString();
                await File.WriteAllTextAsync(filePath, newCode);
            }
        }

        return result;
    }

    /// <summary>
    /// 构建行号到翻译的映射表
    /// </summary>
    private Dictionary<int, Translation> BuildReplacementMap(
        List<Translation> translations,
        string relativePath)
    {
        var map = new Dictionary<int, Translation>();

        foreach (var translation in translations)
        {
            // 只处理已翻译的内容（target != source）
            if (string.IsNullOrWhiteSpace(translation.Target) ||
                translation.Target == translation.Source)
            {
                continue;
            }

            foreach (var context in translation.Contexts)
            {
                if (context.FilePath == relativePath)
                {
                    map[context.LineNumber] = translation;
                }
            }
        }

        return map;
    }

    /// <summary>
    /// 字符串替换 SyntaxRewriter
    /// </summary>
    private class StringReplacementRewriter : CSharpSyntaxRewriter
    {
        private readonly Dictionary<int, Translation> _replacementMap;
        private readonly InterpolationHandler _interpolationHandler;
        private readonly ReplacementResult _result;

        public StringReplacementRewriter(
            Dictionary<int, Translation> replacementMap,
            InterpolationHandler interpolationHandler,
            ReplacementResult result)
        {
            _replacementMap = replacementMap;
            _interpolationHandler = interpolationHandler;
            _result = result;
        }

        /// <summary>
        /// 访问字符串字面量
        /// </summary>
        public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var lineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                if (_replacementMap.TryGetValue(lineNumber, out var translation))
                {
                    // 验证原文匹配
                    var originalText = node.Token.ValueText;
                    if (originalText == translation.Source)
                    {
                        // 创建新的字符串字面量 token（会自动添加引号）
                        var newToken = SyntaxFactory.Literal(translation.Target)
                            .WithLeadingTrivia(node.Token.LeadingTrivia)
                            .WithTrailingTrivia(node.Token.TrailingTrivia);

                        var newNode = SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            newToken);

                        _result.ReplacementCount++;
                        return newNode;
                    }
                }
            }

            return base.VisitLiteralExpression(node);
        }

        /// <summary>
        /// 访问字符串插值表达式
        /// </summary>
        public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            var lineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            if (_replacementMap.TryGetValue(lineNumber, out var translation))
            {
                // 转换当前插值为模板以验证匹配
                var (template, _) = _interpolationHandler.ConvertInterpolationToTemplate(node);

                if (template == translation.Source && translation.Type == StringType.Interpolation)
                {
                    try
                    {
                        // 从翻译的模板重建插值表达式
                        var newNode = _interpolationHandler.ConvertTemplateToInterpolation(
                            translation.Target,
                            translation.Placeholders);

                        // 保留原节点的 trivia（空白、注释等）
                        newNode = newNode
                            .WithLeadingTrivia(node.GetLeadingTrivia())
                            .WithTrailingTrivia(node.GetTrailingTrivia());

                        _result.ReplacementCount++;
                        return newNode;
                    }
                    catch (Exception ex)
                    {
                        _result.Errors.Add($"Line {lineNumber}: Failed to rebuild interpolation - {ex.Message}");
                    }
                }
            }

            return base.VisitInterpolatedStringExpression(node);
        }
    }
}

/// <summary>
/// 替换结果
/// </summary>
public class ReplacementResult
{
    /// <summary>
    /// 修改的文件数
    /// </summary>
    public int FilesModified { get; set; }

    /// <summary>
    /// 替换次数
    /// </summary>
    public int ReplacementCount { get; set; }

    /// <summary>
    /// 总替换数
    /// </summary>
    public int TotalReplacements => ReplacementCount;

    /// <summary>
    /// 错误列表
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 合并另一个结果
    /// </summary>
    public void Merge(ReplacementResult other)
    {
        FilesModified += other.FilesModified;
        ReplacementCount += other.ReplacementCount;
        Errors.AddRange(other.Errors);
    }
}
