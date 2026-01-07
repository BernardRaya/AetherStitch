using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AetherStitch.Models;

namespace AetherStitch.Analyzers;

/// <summary>
/// 处理字符串插值的转换：插值表达式 ↔ 模板 + 占位符
/// </summary>
public class InterpolationHandler
{
    /// <summary>
    /// 将插值字符串转换为模板格式和占位符列表
    /// </summary>
    /// <param name="node">插值字符串语法节点</param>
    /// <returns>模板字符串和占位符信息列表</returns>
    public (string template, List<PlaceholderInfo> placeholders) ConvertInterpolationToTemplate(
        InterpolatedStringExpressionSyntax node)
    {
        var template = string.Empty;
        var placeholders = new List<PlaceholderInfo>();
        var placeholderIndex = 0;

        foreach (var content in node.Contents)
        {
            if (content is InterpolatedStringTextSyntax textSyntax)
            {
                // 普通文本部分
                template += textSyntax.TextToken.ValueText;
            }
            else if (content is InterpolationSyntax interpolation)
            {
                // 插值部分：提取表达式并替换为占位符
                var expression = ExtractInterpolationExpression(interpolation);

                placeholders.Add(new PlaceholderInfo
                {
                    Index = placeholderIndex,
                    Expression = expression,
                    PlaceholderToken = $"{{{placeholderIndex}}}"
                });

                template += $"{{{placeholderIndex}}}";
                placeholderIndex++;
            }
        }

        return (template, placeholders);
    }

    /// <summary>
    /// 从模板和占位符重建插值字符串语法节点
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="placeholders">占位符信息列表</param>
    /// <returns>插值字符串表达式语法节点</returns>
    public InterpolatedStringExpressionSyntax ConvertTemplateToInterpolation(
        string template,
        List<PlaceholderInfo> placeholders)
    {
        var contents = new List<InterpolatedStringContentSyntax>();
        var currentIndex = 0;

        while (currentIndex < template.Length)
        {
            // 查找下一个占位符
            var nextPlaceholderPos = template.IndexOf('{', currentIndex);

            if (nextPlaceholderPos == -1)
            {
                // 没有更多占位符，添加剩余的文本
                var remainingText = template.Substring(currentIndex);
                if (!string.IsNullOrEmpty(remainingText))
                {
                    contents.Add(CreateTextSyntax(remainingText));
                }
                break;
            }

            // 添加占位符之前的文本
            if (nextPlaceholderPos > currentIndex)
            {
                var text = template.Substring(currentIndex, nextPlaceholderPos - currentIndex);
                contents.Add(CreateTextSyntax(text));
            }

            // 解析占位符索引
            var closeBracePos = template.IndexOf('}', nextPlaceholderPos);
            if (closeBracePos == -1)
            {
                throw new InvalidOperationException($"Unclosed placeholder at position {nextPlaceholderPos}");
            }

            var placeholderText = template.Substring(nextPlaceholderPos + 1, closeBracePos - nextPlaceholderPos - 1);
            if (int.TryParse(placeholderText, out var index))
            {
                // 找到对应的占位符信息
                var placeholder = placeholders.FirstOrDefault(p => p.Index == index);
                if (placeholder != null)
                {
                    // 创建插值语法
                    var interpolation = CreateInterpolation(placeholder.Expression);
                    contents.Add(interpolation);
                }
            }

            currentIndex = closeBracePos + 1;
        }

        // 创建插值字符串表达式
        return SyntaxFactory.InterpolatedStringExpression(
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken),
            SyntaxFactory.List(contents),
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringEndToken));
    }

    /// <summary>
    /// 提取插值表达式的完整源代码（包括格式化符号）
    /// </summary>
    private string ExtractInterpolationExpression(InterpolationSyntax interpolation)
    {
        var expression = interpolation.Expression.ToString();

        // 如果有对齐说明，添加它
        if (interpolation.AlignmentClause != null)
        {
            expression += interpolation.AlignmentClause.ToString();
        }

        // 如果有格式化字符串，添加它
        if (interpolation.FormatClause != null)
        {
            expression += interpolation.FormatClause.ToString();
        }

        return expression;
    }

    /// <summary>
    /// 创建文本语法节点
    /// </summary>
    private InterpolatedStringTextSyntax CreateTextSyntax(string text)
    {
        var textToken = SyntaxFactory.Token(
            SyntaxFactory.TriviaList(),
            SyntaxKind.InterpolatedStringTextToken,
            text,
            text,
            SyntaxFactory.TriviaList());

        return SyntaxFactory.InterpolatedStringText(textToken);
    }

    /// <summary>
    /// 从表达式字符串创建插值语法节点
    /// </summary>
    private InterpolationSyntax CreateInterpolation(string expressionText)
    {
        // 分离表达式、对齐和格式化部分
        var alignmentClause = (InterpolationAlignmentClauseSyntax?)null;
        var formatClause = (InterpolationFormatClauseSyntax?)null;

        // 解析格式化符号（如 :yyyy-MM-dd）
        var formatIndex = expressionText.IndexOf(':');
        if (formatIndex > 0)
        {
            var format = expressionText.Substring(formatIndex);
            expressionText = expressionText.Substring(0, formatIndex);

            var formatToken = SyntaxFactory.Token(
                SyntaxFactory.TriviaList(),
                SyntaxKind.InterpolatedStringTextToken,
                format.Substring(1), // 移除 ':'
                format.Substring(1),
                SyntaxFactory.TriviaList());

            formatClause = SyntaxFactory.InterpolationFormatClause(
                SyntaxFactory.Token(SyntaxKind.ColonToken),
                formatToken);
        }

        // 解析对齐说明（如 ,10）
        var alignmentIndex = expressionText.IndexOf(',');
        if (alignmentIndex > 0)
        {
            var alignment = expressionText.Substring(alignmentIndex + 1);
            expressionText = expressionText.Substring(0, alignmentIndex);

            var alignmentExpression = SyntaxFactory.ParseExpression(alignment);
            alignmentClause = SyntaxFactory.InterpolationAlignmentClause(
                SyntaxFactory.Token(SyntaxKind.CommaToken),
                alignmentExpression);
        }

        // 解析表达式
        var expression = SyntaxFactory.ParseExpression(expressionText);

        return SyntaxFactory.Interpolation(expression, alignmentClause, formatClause);
    }

    /// <summary>
    /// 验证占位符是否有效
    /// </summary>
    public bool ValidatePlaceholders(string template, List<PlaceholderInfo> placeholders, out string errorMessage)
    {
        errorMessage = string.Empty;

        // 检查占位符索引的连续性
        var expectedIndices = Enumerable.Range(0, placeholders.Count).ToHashSet();
        var actualIndices = placeholders.Select(p => p.Index).ToHashSet();

        if (!expectedIndices.SetEquals(actualIndices))
        {
            errorMessage = "Placeholder indices are not continuous or have duplicates";
            return false;
        }

        // 检查模板中的占位符数量
        var placeholderCount = 0;
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '{' && i + 1 < template.Length && template[i + 1] != '{')
            {
                placeholderCount++;
            }
        }

        if (placeholderCount != placeholders.Count)
        {
            errorMessage = $"Placeholder count mismatch: template has {placeholderCount}, but {placeholders.Count} placeholders provided";
            return false;
        }

        return true;
    }
}
