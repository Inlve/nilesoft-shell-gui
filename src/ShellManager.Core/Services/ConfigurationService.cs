using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ShellManager.Core.Models;

namespace ShellManager.Core.Services;

public sealed class ConfigurationService(ShellInstallation installation)
{
    public ShellInstallation Installation { get; } = installation;
    public string BackupDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shell Studio", "Backups");

    public IReadOnlyList<ConfigFileModel> LoadFiles()
    {
        var files = new List<ConfigFileModel>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LoadRecursive(Installation.ConfigPath, true, files, visited);
        return files;
    }

    public string Save(ConfigFileModel file, string content)
    {
        var issues = NssParser.Validate(content);
        if (issues.Any(i => i.IsError)) throw new InvalidDataException(string.Join(Environment.NewLine, issues));
        Directory.CreateDirectory(BackupDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var backup = Path.Combine(BackupDirectory, $"{Path.GetFileName(file.Path)}.{stamp}.bak");
        File.Copy(file.Path, backup, true);
        var temp = file.Path + ".shellstudio.tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        File.Move(temp, file.Path, true);
        return backup;
    }

    public void RestartExplorer()
    {
        if (!File.Exists(Installation.ExecutablePath)) throw new FileNotFoundException("找不到 Nilesoft Shell", Installation.ExecutablePath);
        Process.Start(new ProcessStartInfo(Installation.ExecutablePath, "-restart -silent") { UseShellExecute = true });
    }

    public IReadOnlyList<BackupInfo> GetBackups() => Directory.Exists(BackupDirectory)
        ? new DirectoryInfo(BackupDirectory).EnumerateFiles("*.bak")
            .OrderByDescending(f => f.CreationTime).Select(f => new BackupInfo(f.FullName, f.CreationTime, f.Length)).ToList()
        : [];

    public string EnsureManagedFile()
    {
        var importsDirectory = Path.Combine(Path.GetDirectoryName(Installation.ConfigPath)!, "imports");
        Directory.CreateDirectory(importsDirectory);
        var managedPath = Path.Combine(importsDirectory, "shell-studio.nss");
        if (!File.Exists(managedPath))
            File.WriteAllText(managedPath, "// Managed by Shell Studio. Advanced expressions may be edited safely.\r\n\r\n", new UTF8Encoding(false));
        var main = File.ReadAllText(Installation.ConfigPath);
        const string importLine = "import 'imports/shell-studio.nss'";
        if (!main.Contains(importLine, StringComparison.OrdinalIgnoreCase))
        {
            var model = new ConfigFileModel { Path = Installation.ConfigPath, DisplayName = "shell.nss", Content = main, IsMain = true };
            Save(model, main.TrimEnd() + "\r\n\r\n// Entries managed by Shell Studio\r\n" + importLine + "\r\n");
        }
        return managedPath;
    }

    public string SaveTheme(string name, string view, int radius, bool shadow)
    {
        var themeFile = LoadFiles().FirstOrDefault(f => f.Nodes.Any(n => n.Kind == "theme"));
        if (themeFile is null)
        {
            var path = EnsureManagedFile();
            var current = File.ReadAllText(path);
            themeFile = new ConfigFileModel { Path = path, DisplayName = Path.GetFileName(path), Content = current };
            var block = BuildThemeBlock(name, view, radius, shadow, includeSection: true);
            Save(themeFile, current.TrimEnd() + "\r\n\r\n" + block + "\r\n");
            return path;
        }

        const string start = "// <shell-studio-theme>";
        const string end = "// </shell-studio-theme>";
        var content = themeFile.Content;
        var managedStart = content.IndexOf(start, StringComparison.Ordinal);
        var managedEnd = content.IndexOf(end, StringComparison.Ordinal);
        var managedBlock = BuildThemeBlock(name, view, radius, shadow, includeSection: false);
        string updated;
        if (managedStart >= 0 && managedEnd > managedStart)
        {
            updated = content[..managedStart] + managedBlock + content[(managedEnd + end.Length)..];
        }
        else
        {
            var themeNode = themeFile.Nodes.First(n => n.Kind == "theme");
            var open = content.IndexOf('{', Math.Max(0, LineStartIndex(content, themeNode.Line)));
            var close = FindMatchingBrace(content, open);
            if (open < 0 || close < 0) throw new InvalidDataException("无法定位 theme 配置块，请改用源码编辑器。 ");
            updated = content[..close].TrimEnd() + "\r\n\r\n\t" + managedBlock.Replace("\r\n", "\r\n\t").TrimEnd('\t') + "\r\n" + content[close..];
        }
        Save(themeFile, updated);
        return themeFile.Path;
    }

    private static string BuildThemeBlock(string name, string view, int radius, bool shadow, bool includeSection)
    {
        var body = $"// <shell-studio-theme>\r\nname = \"{name}\"\r\nview = view.{view}\r\nitem.radius = {radius}\r\nshadow.enabled = {shadow.ToString().ToLowerInvariant()}\r\n// </shell-studio-theme>";
        return includeSection ? $"theme\r\n{{\r\n\t{body.Replace("\r\n", "\r\n\t")}\r\n}}" : body;
    }

    private static int LineStartIndex(string content, int oneBasedLine)
    {
        var index = 0;
        for (var line = 1; line < oneBasedLine && index < content.Length; line++)
        {
            var next = content.IndexOf('\n', index);
            index = next < 0 ? content.Length : next + 1;
        }
        return index;
    }

    private static int FindMatchingBrace(string content, int open)
    {
        if (open < 0) return -1;
        var depth = 0;
        var quote = '\0';
        for (var i = open; i < content.Length; i++)
        {
            var ch = content[i];
            if (quote != '\0') { if (ch == quote && (i == 0 || content[i - 1] != '\\')) quote = '\0'; continue; }
            if (ch is '\'' or '"') { quote = ch; continue; }
            if (ch == '{') depth++;
            if (ch == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static void LoadRecursive(string path, bool isMain, ICollection<ConfigFileModel> result, ISet<string> visited)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path) || !visited.Add(path)) return;
        var content = File.ReadAllText(path);
        var model = new ConfigFileModel { Path = path, DisplayName = isMain ? "shell.nss · 主配置" : Path.GetFileName(path), Content = content, IsMain = isMain };
        foreach (var node in NssParser.ParseOutline(content)) model.Nodes.Add(node);
        result.Add(model);
        var directory = Path.GetDirectoryName(path)!;
        foreach (var line in content.Replace("\r\n", "\n").Split('\n'))
        {
            var match = NssParser.ImportRegex().Match(line);
            if (!match.Success) continue;
            var value = match.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value;
            if (Regex.IsMatch(value, @"[@$()]")) continue;
            var importPath = Environment.ExpandEnvironmentVariables(value);
            if (!Path.IsPathRooted(importPath)) importPath = Path.Combine(directory, importPath.Replace('/', Path.DirectorySeparatorChar));
            LoadRecursive(importPath, false, result, visited);
        }
    }
}
