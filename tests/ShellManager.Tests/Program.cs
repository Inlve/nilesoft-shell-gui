using ShellManager.Core.Models;
using ShellManager.Core.Services;

var failures = new List<string>();

Check("valid nested menu", NssParser.Validate("menu(title='Tools') { item(title='Open' cmd='app.exe') }").Count == 0);
Check("comments and braces in strings", NssParser.Validate("// { ignored\nitem(title='{ text }' cmd=\"x\")").Count == 0);
Check("detect missing brace", NssParser.Validate("menu(title='Tools') {\n item(title='Open')").Count == 1);
Check("detect missing quote", NssParser.Validate("item(title='Open)").Any(i => i.Message.Contains("字符串")));

var outline = NssParser.ParseOutline("settings\n{\n priority=1\n}\nmenu(title='Tools')\n{\n item(title='Open')\n separator\n}\nimport 'imports/x.nss'");
Check("parse root entries", outline.Count == 3);
Check("parse nested entries", outline[1].Children.Count == 2);
Check("parse menu title", outline[1].Title == "Tools");
Check("quote apostrophe", NssParser.Quote("Bob's tool") == "\"Bob's tool\"");
Check("preserve Windows path", NssSyntax.TryGetLiteral(NssParser.Quote(@"C:\Tools\app.exe"), out var decodedPath) && decodedPath == @"C:\Tools\app.exe");

foreach (var newline in new[] { "\n", "\r\n", "\r" })
{
    var source = string.Join(newline, "settings", "{", "// a comment with {", "priority=1", "}");
    Check("validate newline " + newline.Length, NssParser.Validate(source).Count == 0);
    var broken = source + newline + "item(title='x'))";
    var issue = NssParser.Validate(broken).Single();
    Check("error location with newline " + newline.Length, issue.Line == 6 && issue.Column == 16);
    Check("navigation offset", NssText.CharacterIndexForLine(source, 4) == source.IndexOf("priority", StringComparison.Ordinal));
    var file = new ConfigFileModel { Path = "test.nss", DisplayName = "test", Content = source };
    file.UpdateFromEditor(NssText.Normalize(source).Replace('\n', '\r'));
    Check("loading editor is clean", !file.IsDirty && file.Content == source);
    file.UpdateFromEditor(NssText.Normalize(source).Replace("priority=1", "priority=2").Replace('\n', '\r'));
    Check("real edit is dirty", file.IsDirty && file.Content == source.Replace("priority=1", "priority=2"));
    file.UpdateFromEditor(source);
    Check("undo restores clean state", !file.IsDirty && file.Content == source);
    file.UpdateFromEditor(source + newline + "// edit");
    file.AcceptChanges();
    Check("save establishes new baseline", !file.IsDirty);
}

const string liveDirectory = @"C:\Program Files\Nilesoft Shell";
var losslessSource = "// heading\r\nmenu(\r\n title = 'Tools' /* keep */ where=(sel.count or wnd.is_taskbar)\r\n) { item(title=title.terminal admin image cmd='C:\\Tools\\') separator }\r\ntheme { background { color=auto } item { radius=2 } }";
var syntax = NssSyntax.Parse(losslessSource);
Check("lossless token roundtrip", string.Concat(syntax.Tokens.Select(t => t.Text)) == losslessSource);
Check("multiline and inline blocks", !syntax.HasErrors && syntax.Nodes.Count == 2 && syntax.Nodes[0].Children.Count == 2);
Check("nested theme blocks", syntax.Nodes[1].Children[1].Children[0].Properties[0].Expression == "2");
var menuTitle = syntax.Nodes[0].Properties[0];
var updatedSource = syntax.ReplaceProperty(menuTitle, NssSyntax.QuoteLiteral("Bob's Tools"));
Check("minimal property patch", updatedSource == losslessSource.Replace("'Tools'", "\"Bob's Tools\""));
Check("no-op preserves quote style", syntax.ReplaceProperty(menuTitle, menuTitle.Expression) == losslessSource);
Check("title expression preserved", syntax.Nodes[0].Children[0].Properties[0].Expression == "title.terminal");
Check("bare flags distinct", syntax.Nodes[0].Children[0].Properties.Count(p => p.IsFlag) == 2);
Check("single quoted trailing backslash", NssSyntax.TryGetLiteral(syntax.Nodes[0].Children[0].Properties[^1].Expression, out var trailingPath) && trailingPath == "C:\\Tools\\");
Check("interpolation not a literal", !NssSyntax.TryGetLiteral("'@sel.path'", out _));
Check("quick-add keeps interpolation", NssSyntax.QuoteInterpolated("\"@sel.path\"") == "'\"@sel.path\"'");
foreach (var injected in new[] { "'x' cmd='evil'", "'x') item(title='evil'", "", "'x' // comment" })
{
    try { syntax.ReplaceProperty(menuTitle, injected); Check("reject invalid property edit: " + injected, false); }
    catch (InvalidDataException) { }
}
try { NssSyntax.Parse(losslessSource).ReplaceProperty(menuTitle, "'stale'"); Check("reject stale property snapshot", false); }
catch (InvalidOperationException) { }
Check("imports ignore comments", NssSyntax.Parse("/* import 'bad.nss' */ import 'ok.nss'\n// import 'bad2.nss'").Descendants().Count(n => n.ImportExpression is not null) == 1);
Check("same line imports and entries", NssSyntax.Parse("import 'ok.nss' item(title='x')").Nodes.Count == 2);
Check("missing property value", NssSyntax.Parse("item(title= cmd='x')").HasErrors);
Check("comment error", NssSyntax.Parse("/* never closed").HasErrors);
Check("nesting limit", NssSyntax.Parse(string.Concat(Enumerable.Repeat("theme{", 140)) + new string('}', 140)).HasErrors);
if (Directory.Exists(liveDirectory))
{
    foreach (var path in Directory.EnumerateFiles(liveDirectory, "*.nss", SearchOption.AllDirectories))
    {
        var content = File.ReadAllText(path);
        var liveSyntax = NssSyntax.Parse(content);
        Check("live lossless: " + Path.GetFileName(path), string.Concat(liveSyntax.Tokens.Select(t => t.Text)) == content);
        Check("live config: " + Path.GetFileName(path), NssParser.Validate(content).Count == 0);
        Check("live config in WinUI editor: " + Path.GetFileName(path), NssParser.Validate(NssText.Normalize(content).Replace('\n', '\r')).Count == 0);
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures.Select(f => "FAIL: " + f)));
    return 1;
}

Console.WriteLine("All Shell Studio smoke tests passed.");
return 0;

void Check(string name, bool condition)
{
    if (!condition) failures.Add(name);
}
