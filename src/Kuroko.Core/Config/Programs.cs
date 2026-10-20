namespace Kuroko.Core.Config;

/// <summary>How a program's <see cref="ProgramDefinition.Run"/> is started.</summary>
public enum ProgramKind
{
    /// <summary>An absolute path to an .exe: started directly, without the shell.</summary>
    Executable,

    /// <summary>A program name with .exe and no folder ("wt.exe"): Windows looks it up as Win+R does (App Paths, PATH).</summary>
    Name,

    /// <summary>An absolute path to a shortcut (.lnk): opened by the shell.</summary>
    Shortcut,

    /// <summary>A Store app or other entry of the Start menu: shell:AppsFolder\&lt;AppID&gt;.</summary>
    AppsFolder,
}

/// <summary>A program ([programs.name] in keybinds.toml) that a hotkey or a submap key ("@name") starts.</summary>
/// <param name="Run">What is started, with its %VARIABLES% already replaced.</param>
/// <param name="Args">Arguments, each passed on as written (no variables, no shell).</param>
/// <param name="Dir">Working folder with its %VARIABLES% replaced; null for the default (<see cref="ProgramRules.DefaultDir"/>).</param>
/// <param name="Hotkey">The hotkey with its [mods] variables filled in; null when the program is only reached through a submap.</param>
/// <param name="Line">Line of the [programs.name] header, for messages.</param>
public sealed record ProgramDefinition(
    string Name, string Run, ProgramKind Kind, IReadOnlyList<string> Args, string? Dir = null, string? Hotkey = null, int? Line = null);

/// <summary>What <see cref="ProgramDefinition.Run"/> may be, and where a program starts.</summary>
public static class ProgramRules
{
    public const string AppsFolderPrefix = @"shell:AppsFolder\";

    /// <summary>Script types that are refused for now: they would need a shell or an interpreter.</summary>
    public static IReadOnlyList<string> ScriptExtensions { get; } = [".bat", ".cmd", ".ps1", ".vbs"];

    /// <summary>
    /// The kind of <paramref name="run"/> (variables already replaced), or null with an <paramref name="error"/> that says why
    /// it cannot be started.
    /// </summary>
    public static ProgramKind? Classify(string run, out string error)
    {
        error = string.Empty;
        if (run.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (run.Length > AppsFolderPrefix.Length) return ProgramKind.AppsFolder;
            error = @"shell:AppsFolder\ needs the AppID of the app after it, e.g. shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
            return null;
        }

        var extension = Path.GetExtension(run);
        if (ScriptExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            error = $"scripts ({string.Join(", ", ScriptExtensions)}) are not supported in this test build; start an .exe, a .lnk or shell:AppsFolder\\<AppID>";
            return null;
        }

        if (run.Contains("://", StringComparison.Ordinal))
        {
            error = "URLs are not supported in this test build; start an .exe, a .lnk or shell:AppsFolder\\<AppID>";
            return null;
        }

        // "C:app.exe" is relative to the current folder of drive C, so it counts as a path with a folder.
        var hasFolder = run.Contains('\\') || run.Contains('/') || run.Contains(':');
        if (hasFolder && !IsAbsolute(run))
        {
            error = "a path with a folder must be absolute (C:\\...\\app.exe); a program name alone (\"wt.exe\") is looked up by Windows";
            return null;
        }

        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)) return hasFolder ? ProgramKind.Executable : ProgramKind.Name;
        if (hasFolder && extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return ProgramKind.Shortcut;

        error = hasFolder
            ? "must be an .exe or a .lnk, or shell:AppsFolder\\<AppID>"
            : "a program name without a folder must end in .exe (\"wt.exe\"); otherwise give the absolute path to an .exe or .lnk, or shell:AppsFolder\\<AppID>";
        return null;
    }

    /// <summary>A fully qualified Windows path: "C:\..." or "\\server\share\...". Checked by hand so it means the same on every OS.</summary>
    public static bool IsAbsolute(string path)
        => (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/')
        || path.StartsWith(@"\\", StringComparison.Ordinal);

    /// <summary>
    /// Where the program starts when [programs.name] gives no dir: the folder of an .exe given by path, otherwise the user's
    /// profile folder.
    /// </summary>
    public static string DefaultDir(ProgramDefinition program)
        => program.Kind == ProgramKind.Executable && Path.GetDirectoryName(program.Run) is { Length: > 0 } folder
            ? folder
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Names: letters, digits, _ and -, as submap names.</summary>
    public static bool IsValidName(string name) => name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
}
