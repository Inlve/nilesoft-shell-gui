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
Check("quote apostrophe", NssParser.Quote("Bob's tool") == "'Bob\\'s tool'");
Check("preserve Windows path", NssParser.Quote(@"C:\Tools\app.exe") == @"'C:\Tools\app.exe'");

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
if (Directory.Exists(liveDirectory))
{
    foreach (var path in Directory.EnumerateFiles(liveDirectory, "*.nss", SearchOption.AllDirectories))
    {
        var content = File.ReadAllText(path);
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
