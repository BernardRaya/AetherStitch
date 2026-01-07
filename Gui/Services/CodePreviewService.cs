using AetherStitch.Models;
using System.IO;
using System.Text;

namespace Gui.Services
{
    public class CodePreviewService
    {
        /// <summary>
        /// 读取指定文件的代码片段
        /// </summary>
        public string GetCodeSnippet(string projectPath, ContextReference context, int contextLines = 5)
        {
            try
            {
                var filePath = Path.Combine(projectPath, context.FilePath);
                if (!File.Exists(filePath))
                {
                    return $"文件不存在: {context.FilePath}";
                }

                var lines = File.ReadAllLines(filePath);
                var lineNumber = context.LineNumber - 1; // 转换为0基索引

                if (lineNumber < 0 || lineNumber >= lines.Length)
                {
                    return $"行号 {context.LineNumber} 超出范围";
                }

                // 计算要显示的行范围
                var startLine = Math.Max(0, lineNumber - contextLines);
                var endLine = Math.Min(lines.Length - 1, lineNumber + contextLines);

                // 构建代码片段
                var snippet = new StringBuilder();
                snippet.AppendLine($"// {context.FilePath}:{context.LineNumber}");
                snippet.AppendLine($"// 上下文: {context.CodeContext}");
                snippet.AppendLine();

                for (int i = startLine; i <= endLine; i++)
                {
                    var marker = i == lineNumber ? ">>> " : "    ";
                    snippet.AppendLine($"{marker}{i + 1,4} | {lines[i]}");
                }

                return snippet.ToString();
            }
            catch (Exception ex)
            {
                return $"读取文件失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 在默认编辑器中打开文件
        /// </summary>
        public void OpenInEditor(string projectPath, ContextReference context)
        {
            try
            {
                var filePath = Path.Combine(projectPath, context.FilePath);
                if (File.Exists(filePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception)
            {
                // 忽略错误
            }
        }
    }
}
