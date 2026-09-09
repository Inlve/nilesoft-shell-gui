namespace ShellManager.Core.Services;

public static class NssText
{
    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    public static int CharacterIndexForLine(string text, int oneBasedLine)
    {
        var line = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (line >= oneBasedLine) return i;
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                line++;
            }
            else if (text[i] == '\n') line++;
        }
        return text.Length;
    }
}
