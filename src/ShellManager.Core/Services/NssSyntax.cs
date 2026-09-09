using System.Text;
using ShellManager.Core.Models;

namespace ShellManager.Core.Services;

public enum NssTokenKind { Trivia, Identifier, String, Number, Symbol }
public sealed record NssToken(NssTokenKind Kind, string Text, int Start, int Line, int Column)
{
    public int End => Start + Text.Length;
}

public sealed record NssProperty(string Name, int Start, int ValueStart, int End, string Expression, bool IsFlag = false);

/// <summary>A lossless syntax snapshot. Expressions are preserved, never evaluated.</summary>
public sealed class NssDocument
{
    public string Source { get; }
    public IReadOnlyList<NssToken> Tokens { get; }
    public IReadOnlyList<NssNode> Nodes { get; }
    public IReadOnlyList<ValidationIssue> Diagnostics { get; }
    public bool HasErrors => Diagnostics.Any(d => d.IsError);

    internal NssDocument(string source, IReadOnlyList<NssToken> tokens, IReadOnlyList<NssNode> nodes, IReadOnlyList<ValidationIssue> diagnostics)
        => (Source, Tokens, Nodes, Diagnostics) = (source, tokens, nodes, diagnostics);

    public IEnumerable<NssNode> Descendants() => Walk(Nodes);
    private static IEnumerable<NssNode> Walk(IEnumerable<NssNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Walk(node.Children)) yield return child;
        }
    }

    public string ReplaceProperty(NssProperty property, string expression)
    {
        if (HasErrors) throw new InvalidOperationException("请先修正源码中的语法错误。");
        if (!Descendants().Any(n => n.Properties.Any(p => ReferenceEquals(p, property))))
            throw new InvalidOperationException("属性不属于当前源码快照，请重新选择节点。");
        if (expression == property.Expression) return Source;
        // Parse a synthetic property list to prevent a value edit from injecting siblings or blocks.
        var probe = NssSyntax.Parse("item(__value=" + expression + ")");
        var props = probe.Nodes.FirstOrDefault()?.Properties;
        if (string.IsNullOrWhiteSpace(expression) || probe.HasErrors || probe.Nodes.Count != 1 ||
            props?.Count != 1 || props[0].Name != "__value" || props[0].Expression != expression.Trim())
            throw new InvalidDataException("请输入一个完整的属性值或表达式，不要添加其他属性或注释尾行。");
        var start = property.IsFlag ? property.Start : property.ValueStart;
        var replacement = property.IsFlag ? property.Name + "=" + expression : expression;
        var updated = Source[..start] + replacement + Source[property.End..];
        if (NssSyntax.Parse(updated).HasErrors) throw new InvalidDataException("修改导致配置结构错误，未更新草稿。");
        return updated;
    }
}

public static class NssSyntax
{
    public static NssDocument Parse(string source) => new Parser(source).Parse();

    public static string QuoteInterpolated(string text) => string.Join(" + \"'\" + ", text.Split('\'').Select(part => "'" + part + "'"));

    // Double-quoted NSS strings are literals. Single-quoted strings may interpolate @expressions.
    public static string QuoteLiteral(string text)
    {
        var result = new StringBuilder("\"");
        foreach (var ch in text)
            result.Append(ch switch { '\\' => "\\\\", '"' => "\\\"", '\r' => "\\r", '\n' => "\\n", '\t' => "\\t", '\0' => "\\0", _ => ch.ToString() });
        return result.Append('"').ToString();
    }

    public static bool TryGetLiteral(string expression, out string value)
    {
        value = string.Empty;
        if (expression.Length < 2 || expression[0] is not ('\'' or '"') || expression[^1] != expression[0]) return false;
        var quote = expression[0];
        var body = expression[1..^1];
        if (quote == '\'')
        {
            if (body.Contains('@') || body.Contains('\'')) return false;
            value = body;
            return true;
        }
        var decoded = new StringBuilder();
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] == '"') return false;
            if (body[i] != '\\') { decoded.Append(body[i]); continue; }
            if (++i >= body.Length) return false;
            var escaped = body[i] switch
            {
                '\\' => '\\',
                '"' => '"',
                '\'' => '\'',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '0' => '\0',
                'a' => '\a',
                'b' => '\b',
                'f' => '\f',
                'v' => '\v',
                _ => (char?)null
            };
            // Unsupported escape spellings remain editable as raw expressions without lossy decoding.
            if (escaped is null) return false;
            decoded.Append(escaped.Value);
        }
        value = decoded.ToString();
        return true;
    }

    private sealed class Parser(string source)
    {
        private readonly List<NssToken> _all = [];
        private readonly List<NssToken> _tokens = [];
        private readonly List<ValidationIssue> _issues = [];
        private readonly Dictionary<int, int> _pairs = [];
        private static readonly HashSet<string> Entries = new(StringComparer.OrdinalIgnoreCase) { "item", "menu", "separator", "sep", "modify", "remove" };
        private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase) { "admin", "image", "checked", "disabled", "enabled", "visible", "expanded" };

        public NssDocument Parse()
        {
            Lex();
            var opens = new Stack<int>();
            for (var i = 0; i < _tokens.Count; i++)
            {
                var t = _tokens[i];
                if (t.Kind != NssTokenKind.Symbol) continue;
                if (t.Text is "(" or "{" or "[") opens.Push(i);
                else if (t.Text is ")" or "}" or "]")
                {
                    var expected = t.Text switch { ")" => "(", "}" => "{", _ => "[" };
                    if (opens.Count == 0 || _tokens[opens.Peek()].Text != expected) Error(t, $"意外的闭合符号“{t.Text}”");
                    else _pairs[opens.Pop()] = i;
                }
            }
            foreach (var i in opens.Reverse()) Error(_tokens[i], $"符号“{_tokens[i].Text}”没有闭合");
            var nodes = ParseBody(0, _tokens.Count, 0);
            return new NssDocument(source, _all.AsReadOnly(), nodes, _issues.AsReadOnly());
        }

        private void Lex()
        {
            var i = 0;
            var line = 1;
            var column = 1;
            void Advance()
            {
                var ch = source[i++];
                if (ch == '\r') { line++; column = 1; }
                else if (ch == '\n') { if (i < 2 || source[i - 2] != '\r') line++; column = 1; }
                else column++;
            }
            while (i < source.Length)
            {
                var start = i;
                var startLine = line;
                var startColumn = column;
                var ch = source[i];
                var kind = NssTokenKind.Symbol;
                string? error = null;
                if (char.IsWhiteSpace(ch) || ch == '\uFEFF')
                {
                    kind = NssTokenKind.Trivia;
                    do { Advance(); } while (i < source.Length && char.IsWhiteSpace(source[i]));
                }
                else if (ch == '/' && i + 1 < source.Length && source[i + 1] is '/' or '*')
                {
                    kind = NssTokenKind.Trivia;
                    var block = source[i + 1] == '*';
                    Advance(); Advance();
                    if (!block) { while (i < source.Length && source[i] is not ('\r' or '\n')) Advance(); }
                    else
                    {
                        while (i < source.Length && !(source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/')) Advance();
                        if (i == source.Length) error = "块注释没有闭合";
                        else { Advance(); Advance(); }
                    }
                }
                else if (ch is '\'' or '"')
                {
                    kind = NssTokenKind.String;
                    Advance();
                    while (i < source.Length && source[i] != ch)
                    {
                        if (ch == '"' && source[i] == '\\') { Advance(); if (i < source.Length) Advance(); }
                        else Advance();
                    }
                    if (i == source.Length) error = "字符串没有闭合";
                    else Advance();
                }
                else if (char.IsLetter(ch) || ch is '_' or '$' or '@')
                {
                    kind = NssTokenKind.Identifier;
                    do { Advance(); } while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] is '_' or '.'));
                }
                else if (char.IsDigit(ch))
                {
                    kind = NssTokenKind.Number;
                    do { Advance(); } while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '.'));
                }
                else
                {
                    Advance();
                    if (i < source.Length && (source[start..(i + 1)] is "==" or "!=" or "<=" or ">=" or "&&" or "||" or "+=" or "-=" or "<<" or ">>")) Advance();
                }
                var token = new NssToken(kind, source[start..i], start, startLine, startColumn);
                _all.Add(token);
                if (kind != NssTokenKind.Trivia) _tokens.Add(token);
                if (error is not null) Error(token, error);
            }
        }

        private List<NssNode> ParseBody(int start, int end, int depth)
        {
            var result = new List<NssNode>();
            if (depth > 128)
            {
                if (start < end) Error(_tokens[start], "嵌套层数超过 128，停止解析此区域。");
                return result;
            }
            for (var i = start; i < end;)
            {
                var first = _tokens[i];
                if (first.Kind != NssTokenKind.Identifier) { i = SkipGroup(i, end); continue; }
                var name = first.Text.ToLowerInvariant();
                var node = new NssNode { Kind = name == "sep" ? "separator" : name, Title = first.Text, Line = first.Line, Start = first.Start };
                var next = i + 1;
                if (next < end && _tokens[next].Text == "=")
                {
                    var stop = ValueEnd(next + 1, end, false);
                    node.Properties.Add(Property(i, next, next + 1, stop));
                    node.Kind = first.Text.StartsWith('$') || first.Text.StartsWith('@') ? "variable" : "property";
                    node.End = stop > next + 1 ? _tokens[stop - 1].End : _tokens[next].End;
                    result.Add(node); i = Math.Max(stop, next + 1); continue;
                }
                if (name == "import")
                {
                    var stop = ValueEnd(next, end, false);
                    node.ImportExpression = stop > next ? source[_tokens[next].Start.._tokens[stop - 1].End] : string.Empty;
                    node.Title = "导入 · " + node.ImportExpression;
                    node.End = stop > next ? _tokens[stop - 1].End : first.End;
                    if (stop == next) Error(first, "import 缺少路径表达式。");
                    result.Add(node); i = Math.Max(stop, next); continue;
                }
                if (next < end && _tokens[next].Text == "(" && Entries.Contains(name))
                {
                    if (!_pairs.TryGetValue(next, out var close) || close >= end) { result.Add(node); i = next + 1; continue; }
                    ParseProperties(node, next + 1, close);
                    node.PropertyListEnd = _tokens[close].Start;
                    next = close + 1;
                }
                else if (Entries.Contains(name) && name is not ("separator" or "sep") && (next >= end || _tokens[next].Text != "{"))
                    Error(first, $"{name} 缺少属性括号。");
                if (next < end && _tokens[next].Text == "{")
                {
                    if (_pairs.TryGetValue(next, out var close) && close < end)
                    {
                        node.BodyStart = _tokens[next].Start;
                        node.BodyEnd = _tokens[close].Start;
                        foreach (var child in ParseBody(next + 1, close, depth + 1)) node.Children.Add(child);
                        next = close + 1;
                    }
                }
                if (name == "menu" && node.BodyEnd is null && (next >= end || _tokens[next].Text != "{")) Error(first, "menu 缺少子菜单块。");
                node.End = _tokens[Math.Max(i, next - 1)].End;
                var title = node.Properties.FirstOrDefault(p => p.Name.Equals("title", StringComparison.OrdinalIgnoreCase));
                if (title is not null) node.Title = TryGetLiteral(title.Expression, out var text) ? text : title.Expression;
                result.Add(node);
                i = Math.Max(next, i + 1);
            }
            return result;
        }

        private void ParseProperties(NssNode node, int start, int end)
        {
            for (var i = start; i < end;)
            {
                var token = _tokens[i];
                if (token.Kind != NssTokenKind.Identifier)
                {
                    Error(token, "属性名应为标识符。");
                    i = SkipGroup(i, end); continue;
                }
                if (i + 1 < end && _tokens[i + 1].Text == "=")
                {
                    var stop = ValueEnd(i + 2, end, true);
                    node.Properties.Add(Property(i, i + 1, i + 2, stop));
                    i = Math.Max(stop, i + 2);
                }
                else
                {
                    node.Properties.Add(new NssProperty(token.Text, token.Start, token.End, token.End, string.Empty, true));
                    i++;
                }
            }
        }

        private NssProperty Property(int name, int equals, int start, int stop)
        {
            var token = _tokens[name];
            if (stop <= start)
            {
                Error(_tokens[equals], $"属性 {token.Text} 缺少值。");
                return new NssProperty(token.Text, token.Start, _tokens[equals].End, _tokens[equals].End, string.Empty);
            }
            return new NssProperty(token.Text, token.Start, _tokens[start].Start, _tokens[stop - 1].End, source[_tokens[start].Start.._tokens[stop - 1].End]);
        }

        private int ValueEnd(int start, int end, bool propertyList)
        {
            var i = start;
            while (i < end)
            {
                var token = _tokens[i];
                if (token.Text is "}" or ")" or "]") break;
                if (i > start)
                {
                    var separated = token.Start > _tokens[i - 1].End;
                    var assignment = token.Kind == NssTokenKind.Identifier && i + 1 < end && _tokens[i + 1].Text == "=";
                    if (separated && assignment) break;
                    if (!propertyList && separated && (Entries.Contains(token.Text) || token.Text.Equals("import", StringComparison.OrdinalIgnoreCase))) break;
                    if (!propertyList && token.Line > _tokens[i - 1].Line) break;
                    if (propertyList && separated && Flags.Contains(token.Text) &&
                        (_tokens[i - 1].Kind is NssTokenKind.String or NssTokenKind.Number || _tokens[i - 1].Text is ")" or "]" ||
                         (_tokens[i - 1].Kind == NssTokenKind.Identifier && _tokens[i - 1].Text is not ("and" or "or" or "not")))) break;
                }
                else if (token.Kind == NssTokenKind.Identifier && i + 1 < end && _tokens[i + 1].Text == "=") break;
                i = SkipGroup(i, end);
            }
            return i;
        }

        private int SkipGroup(int i, int end) => _pairs.TryGetValue(i, out var close) && close < end ? close + 1 : i + 1;
        private void Error(NssToken token, string message) => _issues.Add(new ValidationIssue(token.Line, token.Column, message));
    }
}
