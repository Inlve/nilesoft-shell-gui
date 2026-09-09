using System.Collections.ObjectModel;
using System.IO;
using ShellManager.Core.Services;

namespace ShellManager.Core.Models;

public sealed record ShellInstallation(string Directory, string ExecutablePath, string ConfigPath, string? Version)
{
    public string LogPath => Path.Combine(Directory, "shell.log");
    public bool IsDetected => File.Exists(ExecutablePath) && File.Exists(ConfigPath);
}

public sealed class ConfigFileModel
{
    private string _content = string.Empty;
    private string? _savedContent;
    public required string Path { get; init; }
    public required string DisplayName { get; init; }
    public required string Content
    {
        get => _content;
        set { _content = value; _savedContent ??= value; }
    }
    public bool IsMain { get; init; }
    public bool IsDirty => NssText.Normalize(Content) != NssText.Normalize(_savedContent ?? string.Empty);
    public ObservableCollection<NssNode> Nodes { get; } = [];

    public void UpdateFromEditor(string text)
    {
        var normalized = NssText.Normalize(text);
        var saved = _savedContent ?? string.Empty;
        var newline = saved.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : saved.Contains('\r') ? "\r" : "\n";
        Content = normalized == NssText.Normalize(saved) ? saved : normalized.Replace("\n", newline);
    }

    public void AcceptChanges() => _savedContent = Content;
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
