using Kuroko.Core.Config;
using Kuroko.Core.Hotkeys;
using Kuroko.Core.Prompts;

namespace Kuroko.Tests;

/// <summary>[programs.name] in keybinds.toml: reading, every error with its line, the warnings, "@name" in submaps and the hotkey checks.</summary>
public class ProgramConfigTests
{
    private const string Provider = "[providers.x]\ntype = \"openai-compatible\"\nmodel = \"m\"\n";

    private static readonly AppSettings Settings = SettingsLoader.Parse(Provider, _ => null).Settings!;

    private static readonly IReadOnlyList<PromptDefinition> Prompts = [new("Fix", "f"), new("Lockerer", "l")];

    /// <summary>Every absolute path exists unless it contains "missing", so tests do not depend on this machine.</summary>
    private static bool Exists(string path) => !path.Contains("missing", StringComparison.OrdinalIgnoreCase);

    private static KeybindsLoadResult Parse(string toml) => KeybindsLoader.Parse(toml, FakeKeyLayout.German, Exists);

    private static KeybindSettings Load(string toml)
    {
        var result = Parse(toml);
        Assert.True(result.Ok, string.Join("; ", result.Issues));
        return result.Keybinds!;
    }

    private static ConfigIssue SingleError(string toml)
    {
        var result = Parse(toml);
        Assert.False(result.Ok);
        return Assert.Single(result.Issues, i => i.IsError);
    }

    private static List<ConfigIssue> Validate(string toml)
        => ConfigValidator.Validate(Settings, Prompts, Load(toml), keyLayout: FakeKeyLayout.German);

    // ---- reading ----

    [Fact]
    public void The_three_kinds_are_read_with_args_dir_and_the_hotkey_filled_in()
    {
        var keybinds = Load("""
            [mods]
            main = "Ctrl"

            [programs.firefox]
            run = 'C:\Program Files\Mozilla Firefox\firefox.exe'
            hotkey = "$main+Alt+F"

            [programs.terminal]
            run = "wt.exe"
            args = ["-d", "C:\\Projekte", "with \"quotes\" and %PATH%"]
            dir = 'D:\Work'

            [programs.rechner]
            run = 'shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App'

            [programs.notes]
            run = 'C:\Users\Public\Desktop\Notes.lnk'
            hotkey = ""
            """);

        Assert.Equal(["firefox", "terminal", "rechner", "notes"], keybinds.Programs.Select(p => p.Name));

        var firefox = keybinds.Programs[0];
        Assert.Equal(@"C:\Program Files\Mozilla Firefox\firefox.exe", firefox.Run);
        Assert.Equal(ProgramKind.Executable, firefox.Kind);
        Assert.Empty(firefox.Args);
        Assert.Null(firefox.Dir);
        Assert.Equal("Ctrl+Alt+F", HotkeyGesture.Parse(firefox.Hotkey!, FakeKeyLayout.German).ToString(FakeKeyLayout.German));
        Assert.Equal(4, firefox.Line);

        var terminal = keybinds.Programs[1];
        Assert.Equal(ProgramKind.Name, terminal.Kind);
        Assert.Equal(["-d", @"C:\Projekte", "with \"quotes\" and %PATH%"], terminal.Args); // args are passed on unchanged
        Assert.Equal(@"D:\Work", terminal.Dir);
        Assert.Null(terminal.Hotkey);

        Assert.Equal(ProgramKind.AppsFolder, keybinds.Programs[2].Kind);
        Assert.Equal(ProgramKind.Shortcut, keybinds.Programs[3].Kind);
        Assert.Null(keybinds.Programs[3].Hotkey); // "" = no hotkey
        Assert.Same(terminal, keybinds.ProgramOf("TERMINAL"));
        Assert.Null(keybinds.ProgramOf("missing"));
    }

    [Fact]
    public void Variables_are_replaced_in_run_and_dir()
    {
        Environment.SetEnvironmentVariable("KUROKO_TEST_TOOLS", @"C:\Tools");
        var program = Assert.Single(Load("""
            [programs.tool]
            run = '%KUROKO_TEST_TOOLS%\tool.exe'
            dir = '%KUROKO_TEST_TOOLS%\work'
            args = ["%KUROKO_TEST_TOOLS%"]
            """).Programs);

        Assert.Equal(@"C:\Tools\tool.exe", program.Run);
        Assert.Equal(ProgramKind.Executable, program.Kind);
        Assert.Equal(@"C:\Tools\work", program.Dir);
        Assert.Equal(["%KUROKO_TEST_TOOLS%"], program.Args);
    }

    [Fact]
    public void A_keybinds_file_without_programs_reads_as_before()
    {
        var result = Parse(DefaultKeybinds.Create());
        Assert.True(result.Ok, string.Join("; ", result.Issues));
        Assert.Empty(result.Issues);
        Assert.Empty(result.Keybinds!.Programs);
        Assert.Empty(Parse("[app]\noverlay = \"Ctrl+Shift+Space\"\n").Issues);
    }

    [Fact]
    public void The_new_default_file_shows_a_program_only_as_a_comment_that_works_when_uncommented()
    {
        var text = DefaultKeybinds.Create();
        Assert.Contains("# [programs.terminal]", text);

        var uncommented = string.Join('\n', text.Split('\n').Select(l => l.StartsWith("# [programs.") || l.StartsWith("# run =") || l.StartsWith("# args =") || l.StartsWith("# hotkey =") ? l[2..] : l));
        var program = Assert.Single(Load(uncommented).Programs);
        Assert.Equal("wt.exe", program.Run);
        Assert.Equal(["-d", @"C:\Projekte"], program.Args);
    }

    // ---- errors, each with its line ----

    [Theory]
    [InlineData("[programs.a]\nargs = []\n", 1, "run is missing")]
    [InlineData("[programs.a]\nrun = \"\"\n", 2, "run is missing")]
    [InlineData("[programs.a]\nrun = 5\n", 2, "run must be text")]
    [InlineData("[programs.a]\nrun = 'C:\\x\\go.bat'\n", 2, "not supported in this test build")]
    [InlineData("[programs.a]\nrun = 'C:\\x\\go.ps1'\n", 2, "not supported in this test build")]
    [InlineData("[programs.a]\nrun = \"go.cmd\"\n", 2, "not supported in this test build")]
    [InlineData("[programs.a]\nrun = \"go.vbs\"\n", 2, "not supported in this test build")]
    [InlineData("[programs.a]\nrun = 'tools\\app.exe'\n", 2, "must be absolute")]
    [InlineData("[programs.a]\nrun = '%KUROKO_NOT_SET_ANYWHERE%\\app.exe'\n", 2, "must be absolute")]
    [InlineData("[programs.a]\nrun = \"notepad\"\n", 2, "must end in .exe")]
    [InlineData("[programs.a]\nrun = 'C:\\x\\readme.txt'\n", 2, "must be an .exe or a .lnk")]
    [InlineData("[programs.a]\nrun = 'https://example.com'\n", 2, "URLs are not supported in this test build")]
    [InlineData("[programs.a]\nrun = 'shell:AppsFolder\\'\n", 2, "needs the AppID")]
    [InlineData("[programs.a]\nrun = \"wt.exe\"\nargs = \"-d C:\\\\x\"\n", 3, "args must be a list of texts")]
    [InlineData("[programs.a]\nrun = \"wt.exe\"\nargs = [\"-d\", 5]\n", 3, "args must be a list of texts")]
    [InlineData("[programs.a]\nrun = 'shell:AppsFolder\\X!App'\nargs = [\"x\"]\n", 3, "args cannot be passed to a shell:AppsFolder app")]
    [InlineData("[programs.a]\nrun = \"wt.exe\"\ndir = \"\"\n", 3, "dir must not be empty")]
    [InlineData("[programs.a]\nrun = \"wt.exe\"\ndir = 'work'\n", 3, "must be an absolute folder")]
    [InlineData("[programs.a]\nrun = \"wt.exe\"\nhotkey = \"K\"\n", 3, "[programs.a] hotkey")]
    [InlineData("[programs.a]\nrun = \"wt.exe\"\nhotkey = \"$nope+K\"\n", 3, "Unknown variable")]
    [InlineData("[programs.\"my app\"]\nrun = \"wt.exe\"\n", 1, "is not allowed")]
    [InlineData("[programs]\na = \"wt.exe\"\n", 2, "must be a table")]
    [InlineData("programs = 5\n", 1, "programs must contain tables")]
    public void Each_error_names_its_line(string toml, int line, string message)
    {
        var error = SingleError(toml);
        Assert.Equal(line, error.Line);
        Assert.Contains(message, error.Message);
        Assert.Equal(KeybindsLoader.FileName, error.File);
    }

    [Fact]
    public void A_name_used_twice_in_other_case_is_an_error()
    {
        var error = SingleError("[programs.Firefox]\nrun = \"firefox.exe\"\n\n[programs.firefox]\nrun = \"firefox.exe\"\n");
        Assert.Equal(4, error.Line);
        Assert.Contains("used twice", error.Message);
    }

    [Fact]
    public void An_error_in_a_program_rejects_the_whole_file()
    {
        var result = Parse("[app]\noverlay = \"Ctrl+Shift+Space\"\n\n[programs.a]\nrun = \"go.bat\"\n");
        Assert.False(result.Ok);
        Assert.Null(result.Keybinds);
    }

    // ---- warnings ----

    [Fact]
    public void An_unknown_field_is_a_warning()
    {
        var result = Parse("[programs.a]\nrun = \"wt.exe\"\narg = [\"x\"]\n");
        Assert.True(result.Ok);
        var warning = Assert.Single(result.Issues);
        Assert.False(warning.IsError);
        Assert.Equal(3, warning.Line);
        Assert.Contains("unknown setting 'arg'", warning.Message);
    }

    [Fact]
    public void An_absolute_path_that_does_not_exist_is_only_a_warning_and_the_program_stays()
    {
        var result = Parse("[programs.a]\nrun = 'C:\\missing\\app.exe'\ndir = 'C:\\missing-too'\n\n[programs.b]\nrun = 'C:\\missing\\b.lnk'\n");

        Assert.True(result.Ok, string.Join("; ", result.Issues));
        Assert.Equal(2, result.Keybinds!.Programs.Count);
        Assert.Equal([2, 3, 6], result.Issues.Select(i => i.Line));
        Assert.All(result.Issues, i => Assert.False(i.IsError));
        Assert.All(result.Issues, i => Assert.Contains("does not exist right now", i.Message));
    }

    [Fact]
    public void Names_and_store_apps_are_not_looked_up_while_loading()
    {
        var result = Parse("[programs.a]\nrun = \"missing.exe\"\n\n[programs.b]\nrun = 'shell:AppsFolder\\missing!App'\n");
        Assert.True(result.Ok);
        Assert.Empty(result.Issues);
    }

    // ---- submap targets ----

    [Fact]
    public void A_submap_key_with_an_at_names_a_program_and_other_targets_are_unchanged()
    {
        const string Toml = """
            [programs.Firefox]
            run = "firefox.exe"

            [submaps.start]
            open = "Ctrl+Alt+Space"
            keys = { F = "@firefox", K = "Fix", U = "/undo" }
            """;

        var keys = Assert.Single(Load(Toml).Submaps).Keys;

        Assert.True(keys[0].IsProgram);
        Assert.Equal("firefox", keys[0].ProgramName);
        Assert.False(keys[0].IsPrompt);
        Assert.False(keys[0].IsCommand);
        Assert.True(keys[1].IsPrompt);
        Assert.False(keys[1].IsProgram);
        Assert.True(keys[2].IsCommand);
        Assert.False(keys[2].IsProgram);
        Assert.Equal(string.Empty, keys[1].ProgramName);

        // "@firefox" is not a prompt, so prompts.toml need not have it.
        Assert.Empty(Validate(Toml));
    }

    [Fact]
    public void A_submap_key_naming_an_unknown_program_is_an_error_on_its_line()
    {
        var error = SingleError("""
            [programs.firefox]
            run = "firefox.exe"

            [submaps.start]
            open = "Ctrl+Alt+Space"

            [submaps.start.keys]
            F = "@firefox"
            T = "@terminal"
            """);

        Assert.Equal(9, error.Line);
        Assert.Contains("no program 'terminal'", error.Message);
        Assert.Contains("@firefox", error.Message);
    }

    [Fact]
    public void A_bare_at_is_an_error()
    {
        var error = SingleError("[submaps.s]\nopen = \"Ctrl+Alt+Space\"\nkeys = { F = \"@\" }\n");
        Assert.Contains("a program such as \"@firefox\"", error.Message);
    }

    [Fact]
    public void A_program_rejected_for_its_own_error_does_not_add_an_error_for_its_at_key()
    {
        var result = Parse("[programs.a]\nrun = \"go.bat\"\n\n[submaps.s]\nopen = \"Ctrl+Alt+Space\"\nkeys = { A = \"@a\" }\n");
        Assert.False(result.Ok);
        Assert.Contains(result.Issues, i => i.Message.Contains("not supported in this test build"));
    }

    [Fact]
    public void A_program_does_not_count_as_a_prompt_in_the_picker()
    {
        var keybinds = Load("[programs.Fix]\nrun = \"fix.exe\"\n\n[submaps.s]\nopen = \"Ctrl+Alt+Space\"\nkeys = { F = \"@Fix\" }\n");
        Assert.Null(keybinds.SubmapOf("Fix"));
        Assert.Null(keybinds.ApplyTo(new PromptDefinition("Fix", "f")).Submap);
    }

    // ---- hotkeys against the others ----

    [Theory]
    [InlineData("[app]\noverlay = \"Ctrl+Alt+F\"\n", "[app] overlay and program 'firefox'")]
    [InlineData("[prompts]\nFix = \"Ctrl+Alt+F\"\n", "prompt 'Fix' and program 'firefox'")]
    [InlineData("[submaps.s]\nopen = \"Ctrl+Alt+F\"\nkeys = { K = \"Fix\" }\n", "submap 's' and program 'firefox'")]
    [InlineData("[programs.other]\nrun = \"other.exe\"\nhotkey = \"Alt+Ctrl+F\"\n", "program 'firefox' and program 'other'")]
    public void A_program_hotkey_used_elsewhere_too_is_an_error(string other, string owners)
    {
        var issues = Validate("[programs.firefox]\nrun = \"firefox.exe\"\nhotkey = \"Ctrl+Alt+F\"\n\n" + other);
        var error = Assert.Single(issues, i => i.IsError);
        Assert.Contains("is used twice", error.Message);
        Assert.Contains(owners, error.Message);
        Assert.Equal(KeybindsLoader.FileName, error.File);
    }

    [Fact]
    public void A_program_hotkey_on_plain_ctrl_c_is_refused_with_its_line()
    {
        var error = Assert.Single(Validate("[mods]\nmain = \"Ctrl\"\n\n[programs.copy]\nrun = \"x.exe\"\nhotkey = \"$main+C\"\n"), i => i.IsError);
        Assert.Contains("program 'copy'", error.Message);
        Assert.Contains("used by the app to read and replace text", error.Message);
        Assert.Equal(4, error.Line);
    }

    [Fact]
    public void A_submap_key_that_is_a_program_hotkey_without_modifiers_is_an_error()
    {
        var issues = Validate("[programs.f]\nrun = \"f.exe\"\nhotkey = \"F8\"\n\n[submaps.s]\nopen = \"Ctrl+Alt+Space\"\nkeys = { F8 = \"Fix\" }\n");
        Assert.Contains(issues, i => i.IsError && i.Message.Contains("is also the hotkey of program 'f'"));
    }

    // ---- kinds ----

    [Theory]
    [InlineData(@"C:\a\b.exe", ProgramKind.Executable)]
    [InlineData(@"c:/a/b.EXE", ProgramKind.Executable)]
    [InlineData(@"\\server\share\b.exe", ProgramKind.Executable)]
    [InlineData("b.exe", ProgramKind.Name)]
    [InlineData(@"C:\a\b.lnk", ProgramKind.Shortcut)]
    [InlineData(@"SHELL:APPSFOLDER\X!App", ProgramKind.AppsFolder)]
    public void Run_is_classified(string run, ProgramKind kind) => Assert.Equal(kind, ProgramRules.Classify(run, out _));

    [Theory]
    [InlineData("b.lnk")]
    [InlineData(@"..\b.exe")]
    [InlineData(@"C:b.exe")]
    [InlineData(@"C:\a\b")]
    [InlineData("explorer")]
    public void Other_runs_are_refused(string run)
    {
        Assert.Null(ProgramRules.Classify(run, out var error));
        Assert.NotEmpty(error);
    }
}
