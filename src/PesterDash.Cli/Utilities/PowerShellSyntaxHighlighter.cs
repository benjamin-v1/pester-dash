using System.Text;
using Spectre.Console;

namespace PesterDash.Cli.Utilities;

internal static class PowerShellSyntaxHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "function", "filter", "param", "begin", "process", "end", "if", "else", "elseif",
        "switch", "foreach", "for", "while", "do", "until", "return", "break", "continue",
        "throw", "try", "catch", "finally", "trap", "class", "enum", "using", "namespace",
        "workflow", "parallel", "sequence", "inlinescript", "configuration", "dynamicparam",
        "var", "return", "exit", "default", "in", "and", "or", "not", "xor",
    };

    public static string Highlight(string text, string backgroundOpen, string backgroundClose)
    {
        if (string.IsNullOrEmpty(text))
        {
            return $"{backgroundOpen}{backgroundClose}";
        }

        var builder = new StringBuilder();
        builder.Append(backgroundOpen);

        var index = 0;
        while (index < text.Length)
        {
            var ch = text[index];

            if (ch == '#')
            {
                var end = text.Length;
                AppendForeground(builder, text[index..end], "grey");
                index = end;
                continue;
            }

            if (ch is '\'' or '"')
            {
                var end = ReadStringEnd(text, index);
                AppendForeground(builder, text[index..end], "yellow");
                index = end;
                continue;
            }

            if (ch == '$')
            {
                var end = ReadVariableEnd(text, index);
                AppendForeground(builder, text[index..end], "cyan");
                index = end;
                continue;
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                var end = index + 1;
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '-'))
                {
                    end++;
                }

                var word = text[index..end];
                var color = Keywords.Contains(word) ? "blue" : "white";
                AppendForeground(builder, word, color);
                index = end;
                continue;
            }

            if (char.IsDigit(ch))
            {
                var end = index + 1;
                while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
                {
                    end++;
                }

                AppendForeground(builder, text[index..end], "magenta1");
                index = end;
                continue;
            }

            AppendForeground(builder, ch.ToString(), "white");
            index++;
        }

        builder.Append(backgroundClose);
        return builder.ToString();
    }

    private static void AppendForeground(StringBuilder builder, string text, string color)
    {
        builder.Append('[').Append(color).Append(']');
        builder.Append(Markup.Escape(text));
        builder.Append("[/]");
    }

    private static int ReadStringEnd(string text, int start)
    {
        var quote = text[start];
        var index = start + 1;
        while (index < text.Length)
        {
            if (text[index] == quote && text[index - 1] != '`')
            {
                return index + 1;
            }

            index++;
        }

        return text.Length;
    }

    private static int ReadVariableEnd(string text, int start)
    {
        var index = start + 1;
        if (index < text.Length && text[index] == '{')
        {
            while (index < text.Length && text[index] != '}')
            {
                index++;
            }

            return Math.Min(index + 1, text.Length);
        }

        while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
        {
            index++;
        }

        return index;
    }
}
