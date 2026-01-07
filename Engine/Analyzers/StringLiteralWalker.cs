using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AetherStitch.Models;
using System.Security.Cryptography;
using System.Text;

namespace AetherStitch.Analyzers;

/// <summary>
/// 遍历 C# 语法树提取字符串字面量和插值表达式
/// </summary>
public class StringLiteralWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly string _filePath;
    private readonly string _projectBasePath;
    private readonly List<StringLiteral> _literals;
    private readonly InterpolationHandler _interpolationHandler;

    public StringLiteralWalker(
        SemanticModel semanticModel,
        string filePath,
        string projectBasePath)
        : base(SyntaxWalkerDepth.Node)
    {
        _semanticModel = semanticModel;
        _filePath = filePath;
        _projectBasePath = projectBasePath;
        _literals = new List<StringLiteral>();
        _interpolationHandler = new InterpolationHandler();
    }

    /// <summary>
    /// 访问字符串字面量表达式
    /// </summary>
    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.StringLiteralExpression))
        {
            // 检查是否应该跳过
            if (ShouldSkip(node))
            {
                base.VisitLiteralExpression(node);
                return;
            }

            var text = node.Token.ValueText;

            // 跳过空字符串或纯空白字符串
            if (string.IsNullOrWhiteSpace(text))
            {
                base.VisitLiteralExpression(node);
                return;
            }

            var literal = CreateStringLiteral(node, text, StringType.Literal);
            _literals.Add(literal);
        }

        base.VisitLiteralExpression(node);
    }

    /// <summary>
    /// 访问字符串插值表达式
    /// </summary>
    public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        // 检查是否应该跳过
        if (ShouldSkip(node))
        {
            base.VisitInterpolatedStringExpression(node);
            return;
        }

        // 将插值字符串转换为模板格式
        var (template, placeholders) = _interpolationHandler.ConvertInterpolationToTemplate(node);

        // 跳过空模板
        if (string.IsNullOrWhiteSpace(template))
        {
            base.VisitInterpolatedStringExpression(node);
            return;
        }

        var literal = CreateStringLiteral(node, template, StringType.Interpolation);
        literal.Placeholders = placeholders;

        _literals.Add(literal);

        base.VisitInterpolatedStringExpression(node);
    }

    /// <summary>
    /// 创建 StringLiteral 对象
    /// </summary>
    private StringLiteral CreateStringLiteral(SyntaxNode node, string text, StringType type)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var relativePath = Path.GetRelativePath(_projectBasePath, _filePath).Replace('\\', '/');

        var literal = new StringLiteral
        {
            OriginalText = text,
            Type = type,
            FilePath = relativePath,
            LineNumber = lineSpan.StartLinePosition.Line + 1,
            ColumnNumber = lineSpan.StartLinePosition.Character + 1,
            CodeContext = GetCodeContext(node),
            ParentNode = node.Parent?.Kind().ToString()
        };

        // 生成唯一 ID
        literal.Id = GenerateId(literal);

        return literal;
    }

    /// <summary>
    /// 获取代码上下文（类名.方法名）
    /// </summary>
    private string GetCodeContext(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var @class = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

        // 获取命名空间（支持两种语法）
        var regularNamespace = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        var fileScopedNamespace = node.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();

        var context = new List<string>();

        // 添加命名空间名称
        if (regularNamespace != null)
        {
            context.Add(regularNamespace.Name.ToString());
        }
        else if (fileScopedNamespace != null)
        {
            context.Add(fileScopedNamespace.Name.ToString());
        }

        if (@class != null)
        {
            context.Add(@class.Identifier.Text);
        }

        if (method != null)
        {
            context.Add(method.Identifier.Text);
        }

        return context.Count > 0 ? string.Join(".", context) : "Unknown";
    }

    /// <summary>
    /// 生成唯一 ID（基于文件路径、行号和文本的 SHA256）
    /// </summary>
    private string GenerateId(StringLiteral literal)
    {
        var input = $"{literal.FilePath}|{literal.LineNumber}|{literal.OriginalText}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16];
    }

    /// <summary>
    /// 判断是否应该跳过该节点
    /// </summary>
    private bool ShouldSkip(SyntaxNode node)
    {
        // 跳过 Attribute 中的字符串
        if (node.Ancestors().OfType<AttributeSyntax>().Any())
        {
            return true;
        }

        // 跳过 using 指令
        if (node.Ancestors().OfType<UsingDirectiveSyntax>().Any())
        {
            return true;
        }

        // 跳过 namespace 声明
        if (node.Ancestors().OfType<NamespaceDeclarationSyntax>().Any() ||
            node.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().Any())
        {
            // 但只跳过作为 namespace 名称的字符串，不跳过 namespace 内部的代码
            var parent = node.Parent;
            if (parent is NamespaceDeclarationSyntax || parent is FileScopedNamespaceDeclarationSyntax)
            {
                return true;
            }
        }

        // 检查前面的注释是否包含 [NoLocalize] 标记
        var triviaList = node.GetLeadingTrivia();
        foreach (var trivia in triviaList)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                var commentText = trivia.ToString();
                if (commentText.Contains("[NoLocalize]", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 获取提取的字符串列表
    /// </summary>
    public List<StringLiteral> GetLiterals() => _literals;
}
