using System.IO;
using Microsoft.Win32;
using ShellManager.Core.Models;

namespace ShellManager.Core.Services;

public static class ShellLocator
{
    public static ShellInstallation Locate()
    {
        const string defaultDirectory = @"C:\Program Files\Nilesoft Shell";
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Nilesoft\Shell");
        var configuredPath = key?.GetValue("config") as string;
        var configPath = !string.IsNullOrWhiteSpace(configuredPath)
            ? Environment.ExpandEnvironmentVariables(configuredPath)
            : Path.Combine(defaultDirectory, "shell.nss");
        var directory = Path.GetDirectoryName(configPath) ?? defaultDirectory;
        var executable = Path.Combine(directory, "shell.exe");
        if (!File.Exists(executable)) executable = Path.Combine(defaultDirectory, "shell.exe");
        var version = File.Exists(executable)
            ? System.Diagnostics.FileVersionInfo.GetVersionInfo(executable).FileVersion
            : null;
        return new ShellInstallation(Path.GetDirectoryName(executable)!, executable, configPath, version);
    }
}
