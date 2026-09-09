using ShellManager.Core.Models;

namespace ShellManager.Core.Services;

/// <summary>Compatibility entry points backed by the lossless syntax parser.</summary>
public static class NssParser
{
    public static IReadOnlyList<NssNode> ParseOutline(string text) => NssSyntax.Parse(text).Nodes;
    public static IReadOnlyList<ValidationIssue> Validate(string text) => NssSyntax.Parse(text).Diagnostics;
    public static string Quote(string value) => NssSyntax.QuoteLiteral(value);
}
