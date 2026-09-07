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

const string liveDirectory = @"C:\Program Files\Nilesoft Shell";
if (Directory.Exists(liveDirectory))
{
    foreach (var path in Directory.EnumerateFiles(liveDirectory, "*.nss", SearchOption.AllDirectories))
        Check("live config: " + Path.GetFileName(path), NssParser.Validate(File.ReadAllText(path)).Count == 0);
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
