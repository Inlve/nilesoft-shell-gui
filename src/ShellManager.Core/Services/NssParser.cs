using System.Text;
using System.Text.RegularExpressions;
using ShellManager.Core.Models;

namespace ShellManager.Core.Services;

public static partial class NssParser
{
    [GeneratedRegex(@"^\s*(menu|item|separator|modify|remove|settings|theme|import)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EntryRegex();

    [GeneratedRegex("\\btitle\\s*=\\s*(?:'([^']*)'|\"([^\"]*)\"|([^\\s\\)]+))", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("^\\s*import\\s+(?:'([^']+)'|\"([^\"]+)\")", RegexOptions.IgnoreCase)]
    public static partial Regex ImportRegex();

    public static IReadOnlyList<NssNode> ParseOutline(string text)
    {
        var roots = new List<NssNode>();
        var stack = new Stack<ICollection<NssNode>>();
        stack.Push(roots);
        NssNode? pendingContainer = null;
        var lines = Normalize(text).Split('\n');
        var inBlockComment = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripComments(lines[i], ref inBlockComment).Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('}'))
            {
                if (stack.Count > 1) stack.Pop();
                pendingContainer = null;
                continue;
            }

            var match = EntryRegex().Match(line);
            if (match.Success)
            {
                var kind = match.Groups[1].Value.ToLowerInvariant();
                var titleMatch = TitleRegex().Match(line);
                var title = titleMatch.Success
                    ? titleMatch.Groups.Cast<Group>().Skip(1).FirstOrDefault(g => g.Success)?.Value ?? kind
                    : kind == "import" ? ImportDisplay(line) : FriendlyKind(kind);
                var node = new NssNode { Kind = kind, Title = title, Line = i + 1 };
                stack.Peek().Add(node);
                pendingContainer = kind is "menu" or "settings" or "theme" ? node : null;
            }

            if (line.Contains('{') && pendingContainer is not null)
            {
                stack.Push(pendingContainer.Children);
                pendingContainer = null;
            }
        }
        return roots;
    }

    public static IReadOnlyList<ValidationIssue> Validate(string text)
    {
        text = NssText.Normalize(text);
        var issues = new List<ValidationIssue>();
        var braces = new Stack<(char Ch, int Line, int Column)>();
        var quote = '\0';
        var escape = false;
        var line = 1;
        var column = 0;
        var inLineComment = false;
        var inBlockComment = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            column++;
            if (ch == '\n') { line++; column = 0; inLineComment = false; continue; }
            if (inLineComment) continue;
            if (inBlockComment)
            {
                if (ch == '*' && i + 1 < text.Length && text[i + 1] == '/') { inBlockComment = false; i++; column++; }
                continue;
            }
            if (quote == '\0' && ch == '/' && i + 1 < text.Length)
            {
                if (text[i + 1] == '/') { inLineComment = true; i++; column++; continue; }
                if (text[i + 1] == '*') { inBlockComment = true; i++; column++; continue; }
            }
            if (quote != '\0')
            {
                if (escape) { escape = false; continue; }
                if (ch == '\\') { escape = true; continue; }
                if (ch == quote) quote = '\0';
                continue;
            }
            if (ch is '\'' or '"') { quote = ch; continue; }
            if (ch is '{' or '(' or '[') braces.Push((ch, line, column));
            else if (ch is '}' or ')' or ']')
            {
                var expected = ch switch { '}' => '{', ')' => '(', _ => '[' };
                if (braces.Count == 0 || braces.Peek().Ch != expected)
                    issues.Add(new ValidationIssue(line, column, $"意外的闭合符号“{ch}”"));
                else braces.Pop();
            }
        }

        if (quote != '\0') issues.Add(new ValidationIssue(line, Math.Max(column, 1), "字符串没有闭合"));
        if (inBlockComment) issues.Add(new ValidationIssue(line, Math.Max(column, 1), "块注释没有闭合"));
        foreach (var open in braces.Reverse())
            issues.Add(new ValidationIssue(open.Line, open.Column, $"符号“{open.Ch}”没有闭合"));
        return issues;
    }

    public static string Quote(string value) => $"'{value.Replace("'", "\\'")}'";

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
    private static string FriendlyKind(string kind) => kind switch
    {
        "item" => "未命名菜单项",
        "menu" => "未命名子菜单",
        "separator" => "分隔线",
        "settings" => "全局设置",
        "theme" => "外观主题",
        "modify" => "修改现有项",
        "remove" => "隐藏现有项",
        _ => kind
    };
    private static string ImportDisplay(string line)
    {
        var match = ImportRegex().Match(line);
        return match.Success ? $"导入 · {match.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value}" : "导入文件";
    }
    private static string StripComments(string line, ref bool inBlock)
    {
        var output = new StringBuilder();
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inBlock)
            {
                if (ch == '*' && i + 1 < line.Length && line[i + 1] == '/') { inBlock = false; i++; }
                continue;
            }
            if (quote == '\0' && ch == '/' && i + 1 < line.Length)
            {
                if (line[i + 1] == '/') break;
                if (line[i + 1] == '*') { inBlock = true; i++; continue; }
            }
            if (ch is '\'' or '"') quote = quote == '\0' ? ch : quote == ch ? '\0' : quote;
            output.Append(ch);
        }
        return output.ToString();
    }
}
