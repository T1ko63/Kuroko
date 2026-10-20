using System.IO;
using System.Runtime.InteropServices;
using Kuroko.App;
using Kuroko.Core.Config;
using Kuroko.Core.Hotkeys;
using Kuroko.Core.Localization;
using Kuroko.Core.Prompts;
using Kuroko.Platform.Launching;
using Kuroko.Windows.Tests.Fakes;

namespace Kuroko.Windows.Tests;

/// <summary>Starting programs: the start info for each kind of run, argument quoting on the shell path, and AppController.</summary>
[Collection(UiThread.Collection)]
public class ProgramTests
{
    private static readonly PromptDefinition Fix = new("Fix", "Fix the spelling.");

    private static readonly ProgramDefinition Firefox = new("firefox", @"C:\Program Files\Mozilla Firefox\firefox.exe", ProgramKind.Executable,
        ["-new-window", "https://example.com/a b"]);

    private static readonly ProgramDefinition Terminal = new("terminal", "wt.exe", ProgramKind.Name, ["-d", @"C:\My Projects\"]);

    private static readonly ProgramDefinition Calculator = new("calculator", @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
        ProgramKind.AppsFolder, []);

    private static readonly ProgramDefinition Shortcut = new("notes", @"C:\Users\Public\Desktop\Notes.lnk", ProgramKind.Shortcut, ["a\"b"]);

    private static string UserFolder => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // ---- start info ----

    [Fact]
    public void An_exe_by_path_starts_without_the_shell_with_each_argument_on_its_own_in_its_folder()
    {
        var info = ProgramLauncher.BuildStartInfo(Firefox);

        Assert.False(info.UseShellExecute);
        Assert.Equal(Firefox.Run, info.FileName);
        Assert.Equal(["-new-window", "https://example.com/a b"], info.ArgumentList);
        Assert.Equal(string.Empty, info.Arguments);
        Assert.Equal(@"C:\Program Files\Mozilla Firefox", info.WorkingDirectory);
        Assert.Equal(string.Empty, info.Verb); // never "runas"
    }

    [Fact]
    public void A_program_name_goes_through_the_shell_with_quoted_arguments_in_the_user_folder()
    {
        var info = ProgramLauncher.BuildStartInfo(Terminal);

        Assert.True(info.UseShellExecute);
        Assert.Equal("wt.exe", info.FileName);
        Assert.Empty(info.ArgumentList);
        Assert.Equal(["-d", @"C:\My Projects\"], CommandLineToArgs(info.Arguments));
        Assert.Equal(UserFolder, info.WorkingDirectory);
        Assert.Equal(string.Empty, info.Verb);
    }

    [Fact]
    public void A_shortcut_and_a_store_app_go_through_the_shell()
    {
        var shortcut = ProgramLauncher.BuildStartInfo(Shortcut);
        Assert.True(shortcut.UseShellExecute);
        Assert.Equal(Shortcut.Run, shortcut.FileName);
        Assert.Equal(["a\"b"], CommandLineToArgs(shortcut.Arguments));
        Assert.Equal(UserFolder, shortcut.WorkingDirectory);

        var app = ProgramLauncher.BuildStartInfo(Calculator);
        Assert.True(app.UseShellExecute);
        Assert.Equal(Calculator.Run, app.FileName);
        Assert.Equal(string.Empty, app.Arguments);
        Assert.Equal(UserFolder, app.WorkingDirectory);
    }

    [Fact]
    public void Dir_replaces_the_default_working_folder()
    {
        Assert.Equal(@"D:\Work", ProgramLauncher.BuildStartInfo(Firefox with { Dir = @"D:\Work" }).WorkingDirectory);
        Assert.Equal(@"D:\Work", ProgramLauncher.BuildStartInfo(Terminal with { Dir = @"D:\Work" }).WorkingDirectory);
    }

    public static TheoryData<string[]> ArgumentLists { get; } = new()
    {
        new[] { "plain" },
        new[] { "with space", "two" },
        new[] { "say \"hi\"", "x" },
        new[] { @"C:\trailing slash\", "next" },
        new[] { @"C:\two\\", "\\\"", "a\\\\\"b" },
        new[] { "", "empty before" },
        new[] { "tab\there", "%PATH%", "^&|<>" },
        new[] { "ü ö ß ↔" },
    };

    [Theory]
    [MemberData(nameof(ArgumentLists))]
    public void Arguments_on_the_shell_path_arrive_exactly_as_written(string[] args)
    {
        // ShellExecute appends its parameter string to the program's command line; Windows programs split it again with
        // the rules of CommandLineToArgvW. The program name in front stands for what the shell puts there.
        Assert.Equal(args, CommandLineToArgs(ProgramLauncher.JoinArguments(args)));
    }

    [Fact]
    public void Simple_arguments_are_not_quoted()
        => Assert.Equal(@"-d C:\Projects --flag=1", ProgramLauncher.JoinArguments(["-d", @"C:\Projects", "--flag=1"]));

    [Fact]
    public void A_start_that_fails_returns_the_reason_and_does_not_throw()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"kuroko-missing-{Guid.NewGuid():N}", "nothing.exe");

        var result = ProgramLauncher.Start(new ProgramDefinition("missing", missing, ProgramKind.Executable, [], Dir: Path.GetTempPath()));

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Null(result.ProcessId);
    }

    /// <summary>Splits a parameter string as a Windows program does.</summary>
    private static string[] CommandLineToArgs(string arguments)
    {
        var pointer = CommandLineToArgvW("program.exe " + arguments, out var count);
        Assert.NotEqual(IntPtr.Zero, pointer);
        try
        {
            var args = new string[count - 1];
            for (var i = 1; i < count; i++) args[i - 1] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, i * IntPtr.Size))!;
            return args;
        }
        finally
        {
            LocalFree(pointer);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    // ---- AppController ----

    private sealed class FakeLauncher : IProgramLauncher
    {
        public List<string> Started { get; } = new();

        public string? FailWith { get; set; }

        public ProgramStartResult Start(ProgramDefinition program)
        {
            Started.Add(program.Name);
            return FailWith is null ? ProgramStartResult.Started(1234) : ProgramStartResult.Failed(FailWith);
        }
    }

    private readonly FakeTextAccess _text = new();
    private readonly FakeRunner _runner = new();
    private readonly FakeOverlay _overlay = new();
    private readonly FakeStatus _status = new();
    private readonly FakeHotkeys _hotkeys = new();
    private readonly FakeTextTarget _clipboard = new();
    private readonly FakeDesktop _desktop = new();
    private readonly FakeLauncher _launcher = new();

    private static readonly SubmapDefinition Start = new("start", "Ctrl+Alt+Space", "start", 3000,
    [
        new SubmapKey("F", 'F', "@Firefox"), // program names are not case-sensitive
        new SubmapKey("X", 'X', "@gone"),
        new SubmapKey("K", 'K', "Fix"),
    ]);

    private AppController Controller() => new(_text, _overlay, _status, _runner, () => [Fix],
        hotkeys: _hotkeys, clipboard: _clipboard, desktop: _desktop, programs: () => [Firefox, Terminal], launcher: _launcher);

    [Fact]
    public void The_hotkey_starts_the_program_and_shows_nothing()
    {
        var controller = Controller();

        controller.OnProgramHotkey(0, Firefox);

        Assert.Equal(["firefox"], _launcher.Started);
        Assert.Empty(_status.Shown);
        Assert.Empty(_text.Calls);
        Assert.False(controller.IsBusy);
        Assert.Equal(0, _hotkeys.Count);
    }

    [Fact]
    public void A_submap_key_with_an_at_starts_the_program()
    {
        var controller = Controller();
        controller.OnSubmapHotkey(0, Start);

        Assert.True(_hotkeys.Press(new HotkeyGesture(HotkeyModifiers.None, 'F')));

        Assert.Equal(["firefox"], _launcher.Started);
        Assert.Null(controller.OpenSubmap);
        Assert.Equal(0, _hotkeys.Count);
        Assert.Empty(_text.Calls);
    }

    [Fact]
    public void A_submap_key_for_a_program_that_is_gone_starts_nothing()
    {
        var controller = Controller();
        controller.OnSubmapHotkey(0, Start);

        Assert.True(_hotkeys.Press(new HotkeyGesture(HotkeyModifiers.None, 'X')));

        Assert.Empty(_launcher.Started);
        Assert.Null(controller.OpenSubmap);
    }

    [Fact]
    public void A_failed_start_shows_the_error_pill_with_the_reason()
    {
        var controller = Controller();
        _launcher.FailWith = "The system cannot find the file specified.";

        controller.OnProgramHotkey(0, Firefox);

        Assert.Equal([$"error: {Loc.Get("err_program_start", "firefox", "The system cannot find the file specified.")}"], _status.Shown);
        Assert.True(_status.IsErrorShown);
        Assert.False(controller.IsBusy);
    }

    [Fact]
    public void The_error_message_names_the_program_and_the_reason_in_both_languages()
    {
        var previous = Loc.Language;
        try
        {
            Loc.Language = "de";
            Assert.Equal("Programm 'firefox' konnte nicht gestartet werden: x", Loc.Get("err_program_start", "firefox", "x"));
            Loc.Language = "en";
            Assert.Equal("Program 'firefox' could not be started: x", Loc.Get("err_program_start", "firefox", "x"));
        }
        finally
        {
            Loc.Language = previous;
        }
    }

    [Fact]
    public void Nothing_starts_while_a_run_is_busy() => UiThread.Run(async () =>
    {
        var controller = Controller();
        _runner.AutoAnswer = null;
        controller.OnPromptHotkey(0, Fix);
        await UiThread.Until(() => _runner.Waiting, "AI call");

        controller.OnProgramHotkey(0, Firefox);

        Assert.Empty(_launcher.Started);
        _runner.Answer("done");
        await UiThread.Until(() => !controller.IsBusy, "controller idle");
    });

    [Fact]
    public void Nothing_starts_while_the_overlay_is_open() => UiThread.Run(async () =>
    {
        var controller = Controller();
        controller.OnOverlayHotkey(0);
        await UiThread.Until(() => _overlay.IsShown, "overlay shown");

        controller.OnProgramHotkey(0, Firefox);

        Assert.Empty(_launcher.Started);
        Assert.True(_overlay.IsShown);
        _overlay.Cancel();
        await UiThread.Until(() => !controller.IsBusy, "controller idle");
    });
}
