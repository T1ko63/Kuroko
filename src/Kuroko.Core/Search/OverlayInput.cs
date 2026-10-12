namespace Kuroko.Core.Search;

public enum OverlayInputKind
{
    /// <summary>Prompt search, as the overlay always did.</summary>
    Search,

    /// <summary>Starts with "/": a command name, optionally followed by a value.</summary>
    Command,

    /// <summary>Starts with "//": a one-off question about the selected text.</summary>
    Question,
}

/// <summary>
/// What the text in the overlay's search field means. <see cref="Text"/> is the search query, the command name or the
/// question; <see cref="Value"/> is only used by commands (everything after the name).
/// </summary>
public readonly record struct OverlayInput(OverlayInputKind Kind, string Text, string Value = "")
{
    /// <summary>
    /// The input rules of the overlay, the only place that decides them. Surrounding spaces never count.
    /// "//" makes the rest a question; "/" makes the word up to the first space the command and the rest its value
    /// ("/ copy" is the command "copy"); anything else is a prompt search. Commands split their own value further.
    /// </summary>
    public static OverlayInput Parse(string? input)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.StartsWith("//", StringComparison.Ordinal)) return new(OverlayInputKind.Question, text[2..].Trim());
        if (!text.StartsWith('/')) return new(OverlayInputKind.Search, text);

        var rest = text[1..].TrimStart();
        var space = rest.IndexOfAny([' ', '\t']);
        return space < 0
            ? new(OverlayInputKind.Command, rest)
            : new(OverlayInputKind.Command, rest[..space], rest[(space + 1)..].Trim());
    }
}
