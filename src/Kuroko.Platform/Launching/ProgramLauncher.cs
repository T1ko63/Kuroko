using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Kuroko.Core.Config;

namespace Kuroko.Platform.Launching;

/// <summary>The outcome of starting a program: the process id when Windows gives one, or why the start failed.</summary>
public sealed record ProgramStartResult(bool Success, int? ProcessId, string? Error)
{
    public static ProgramStartResult Started(int? processId) => new(true, processId, null);

    public static ProgramStartResult Failed(string error) => new(false, null, error);
}

/// <summary>
/// Starts a program from [programs] in keybinds.toml and forgets it: no waiting, the process handle is released at once.
/// Never through cmd or another shell, never elevated (no "runas"). An .exe given by path is started directly with its
/// arguments one by one; a program name, a shortcut and a shell:AppsFolder app need the Windows shell (ShellExecute) to be
/// found or opened, and get their arguments quoted by the Windows rules.
/// </summary>
public static class ProgramLauncher
{
    /// <summary>The start info for <paramref name="program"/>; makes no system call, so tests can check it.</summary>
    public static ProcessStartInfo BuildStartInfo(ProgramDefinition program)
    {
        var info = new ProcessStartInfo(program.Run)
        {
            UseShellExecute = program.Kind != ProgramKind.Executable,
            WorkingDirectory = program.Dir ?? ProgramRules.DefaultDir(program),
        };

        if (info.UseShellExecute)
        {
            // ShellExecute takes one parameter string that becomes the rest of the command line; quoted here so every
            // argument arrives as written, whatever the shell hands it to.
            info.Arguments = JoinArguments(program.Args);
        }
        else
        {
            foreach (var arg in program.Args) info.ArgumentList.Add(arg);
        }

        return info;
    }

    /// <summary>Starts <paramref name="program"/>; never throws.</summary>
    public static ProgramStartResult Start(ProgramDefinition program)
    {
        try
        {
            using var process = Process.Start(BuildStartInfo(program));
            int? id = null;
            try
            {
                id = process?.Id;
            }
            catch (InvalidOperationException)
            {
                // The shell handed the start to an already running process; there is no id to report.
            }

            return ProgramStartResult.Started(id);
        }
        catch (Win32Exception ex)
        {
            return ProgramStartResult.Failed(ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return ProgramStartResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Arguments joined into one command line as the Microsoft C runtime and CommandLineToArgvW split it again: an argument
    /// with a blank, a tab or a quote (or an empty one) is put in quotes, a quote inside is escaped with a backslash, and
    /// backslashes are doubled only where they come before a quote.
    /// </summary>
    public static string JoinArguments(IEnumerable<string> args)
    {
        var line = new StringBuilder();
        foreach (var arg in args)
        {
            if (line.Length > 0) line.Append(' ');
            if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0)
            {
                line.Append(arg);
                continue;
            }

            line.Append('"');
            var backslashes = 0;
            foreach (var c in arg)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    line.Append('\\', backslashes * 2 + 1);
                }
                else
                {
                    line.Append('\\', backslashes);
                }

                backslashes = 0;
                line.Append(c);
            }

            line.Append('\\', backslashes * 2).Append('"');
        }

        return line.ToString();
    }
}
