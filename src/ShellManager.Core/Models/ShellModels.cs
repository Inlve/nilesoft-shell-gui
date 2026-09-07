using System.Collections.ObjectModel;
using System.IO;

namespace ShellManager.Core.Models;

public sealed record ShellInstallation(string Directory, string ExecutablePath, string ConfigPath, string? Version)
{
    public string LogPath => Path.Combine(Directory, "shell.log");
    public bool IsDetected => File.Exists(ExecutablePath) && File.Exists(ConfigPath);
}

public sealed class ConfigFileModel
{
    public required string Path { get; init; }
    public required string DisplayName { get; init; }
    public required string Content { get; set; }
    public bool IsMain { get; init; }
    public bool IsDirty { get; set; }
    public ObservableCollection<NssNode> Nodes { get; } = [];
}

public sealed class NssNode
{
    public required string Kind { get; init; }
    public required string Title { get; init; }
    public int Line { get; init; }
    public ObservableCollection<NssNode> Children { get; } = [];
}

public sealed record ValidationIssue(int Line, int Column, string Message, bool IsError = true)
{
    public override string ToString() => $"第 {Line} 行，第 {Column} 列：{Message}";
}

public sealed record BackupInfo(string Path, DateTime CreatedAt, long Size);
