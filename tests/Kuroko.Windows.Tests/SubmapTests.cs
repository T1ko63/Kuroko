using Kuroko.App;
using Kuroko.Core.Config;
using Kuroko.Core.Hotkeys;
using Kuroko.Core.Localization;
using Kuroko.Core.Prompts;
using Kuroko.Windows.Tests.Fakes;

namespace Kuroko.Windows.Tests;

/// <summary>
/// Submaps in AppController: the label in the pill, the keys and Esc as temporary hotkeys, and that every way out
/// (a listed key, Esc, the timeout, a click, another hotkey, a reload, an error) releases all of them.
/// </summary>
[Collection(UiThread.Collection)]
public class SubmapTests
{
    private static readonly PromptDefinition Professional = new("Professioneller", "Make it professional.");
    private static readonly PromptDefinition Casual = new("Lockerer", "Make it casual.");

    private static HotkeyGesture Key(char c) => new(HotkeyModifiers.None, c);

    private static readonly SubmapDefinition Tone = new("tone", "Alt+Shift+T", "Tone: P L", 1500,
    [
        new SubmapKey("P", 'P', "Professioneller"),
        new SubmapKey("l", 'L', "lockerer"), // prompt names are not case-sensitive
        new SubmapKey("U", 'U', "/undo"),
        new SubmapKey("T", 'T', "/test"),
    ]);

    private readonly FakeTextAccess _text = new();
    private readonly FakeRunner _runner = new();
    private readonly FakeOverlay _overlay = new();
    private readonly FakeStatus _status = new();
    private readonly FakeHotkeys _hotkeys = new();
    private readonly FakeTextTarget _clipboard = new();
    private readonly FakeDesktop _desktop = new();
    private int _tests;

    private AppController Controller() => new(_text, _overlay, _status, _runner, () => [Professional, Casual],
        hotkeys: _hotkeys, clipboard: _clipboard, desktop: _desktop,
        commands: () => [new AppCommand(CommandNames.Test, "Test", () => _tests++, CommandFocus.Field)]);

    private void AssertClosed(AppController controller)
    {
        Assert.Null(controller.OpenSubmap);
        Assert.Equal(0, _hotkeys.Count);
        Assert.Empty(_desktop.RunningTimers);
    }

    [Fact]
    public void Opening_shows_the_label_and_takes_the_listed_keys_and_esc_for_the_timeout()
    {
        var controller = Controller();

        controller.OnSubmapHotkey(0, Tone);

        Assert.Same(Tone, controller.OpenSubmap);
        Assert.Equal(["submap: Tone: P L"], _status.Shown);
        foreach (var key in "PLUT") Assert.True(_hotkeys.IsRegistered(Key(key)), $"{key} not registered");
        Assert.True(_hotkeys.IsRegistered(FakeHotkeys.Esc));
        Assert.Equal(5, _hotkeys.Count);
        Assert.Equal([TimeSpan.FromMilliseconds(1500)], _desktop.RunningTimers);
        Assert.True(controller.IsBusy);
        Assert.False(_hotkeys.Press(Key('X')), "a key that is not listed must stay with the app in front");
    }

    [Fact]
    public void A_listed_key_closes_the_submap_and_runs_the_prompt_as_its_hotkey_would() => UiThread.Run(async () =>
    {
        var controller = Controller();
        _runner.AutoAnswer = "the text";
        controller.OnSubmapHotkey(0, Tone);

        Assert.True(_hotkeys.Press(Key('L')));
        await UiThread.Until(() => !controller.IsBusy, "run");

        Assert.Equal([("teh text", "the text")], _text.Replacements);
        Assert.Equal(["submap: Tone: P L", "hide", $"progress: {Loc.Get("working", Casual.Name)}", "hide"], _status.Shown);
        AssertClosed(controller);
    });

    [Fact]
    public void Esc_closes_the_submap_and_runs_nothing()
    {
        var controller = Controller();
        controller.OnSubmapHotkey(0, Tone);

        Assert.True(_hotkeys.Press(FakeHotkeys.Esc));

        AssertClosed(controller);
        Assert.Equal(["submap: Tone: P L", "hide"], _status.Shown);
        Assert.Empty(_text.Calls);
        Assert.False(_hotkeys.Press(Key('P')));
    }

    [Fact]
    public void The_timeout_closes_the_submap_and_runs_nothing()
    {
        var controller = Controller();
        controller.OnSubmapHotkey(0, Tone);

        _desktop.ElapseTimers();

        AssertClosed(controller);
        Assert.Equal(["submap: Tone: P L", "hide"], _status.Shown);
        Assert.Empty(_text.Calls);
    }

    [Fact]
    public void A_click_on_the_pill_closes_the_submap()
    {
        var controller = Controller();
        controller.OnSubmapHotkey(0, Tone);

        _status.Click();

        AssertClosed(controller);
        Assert.Empty(_text.Calls);
    }

    [Fact]
    public void The_open_hotkey_again_closes_the_submap()
    {
        var controller = Controller();
        controller.OnSubmapHotkey(0, Tone);

        controller.OnSubmapHotkey(0, Tone);

        AssertClosed(controller);
    }

    [Fact]
    public void Another_submap_replaces_the_open_one()
    {
        var controller = Controller();
        var other = new SubmapDefinition("other", "Alt+Shift+O", "Other", 2000, [new SubmapKey("K", 'K', "Lockerer")]);
        controller.OnSubmapHotkey(0, Tone);

        controller.OnSubmapHotkey(0, other);

        Assert.Same(other, controller.OpenSubmap);
        Assert.False(_hotkeys.IsRegistered(Key('P')));
        Assert.True(_hotkeys.IsRegistered(Key('K')));
        Assert.Equal(2, _hotkeys.Count);
        Assert.Single(_desktop.RunningTimers);
    }

    [Fact]
    public void A_key_another_app_holds_is_left_out_and_the_others_still_work() => UiThread.Run(async () =>
    {
        var controller = Controller();
        _hotkeys.Taken.Add(Key('P'));
        _runner.AutoAnswer = "x";

        controller.OnSubmapHotkey(0, Tone);
        Assert.False(_hotkeys.IsRegistered(Key('P')));
        Assert.True(_hotkeys.IsRegistered(Key('L')));

        Assert.True(_hotkeys.Press(Key('L')));
        await UiThread.Until(() => !controller.IsBusy, "run");
        Assert.Single(_text.Replacements);
        AssertClosed(controller);
    });

    [Fact]
    public void The_open_hotkey_is_ignored_while_a_run_is_active() => UiThread.Run(async () =>
    {
        var controller = Controller();
        _runner.AutoAnswer = null;
        controller.OnPromptHotkey(0, Professional);
        await UiThread.Until(() => _runner.Waiting, "AI call");

        controller.OnSubmapHotkey(0, Tone);

        Assert.Null(controller.OpenSubmap);
        Assert.False(_hotkeys.IsRegistered(Key('P')));
        Assert.DoesNotContain(_status.Shown, s => s.StartsWith("submap"));
        _runner.Answer("done");
        await UiThread.Until(() => !controller.IsBusy, "run");
        Assert.Equal(0, _hotkeys.Count);
    });

    [Fact]
    public void Another_hotkey_closes_the_submap_before_it_starts() => UiThread.Run(async () =>
    {
        var controller = Controller();
        _runner.AutoAnswer = "x";
        controller.OnSubmapHotkey(0, Tone);

        controller.OnPromptHotkey(0, Professional);

        Assert.Null(controller.OpenSubmap);
        Assert.False(_hotkeys.IsRegistered(Key('P')));
        await UiThread.Until(() => !controller.IsBusy, "run");
        AssertClosed(controller);
    });

    [Fact]
    public void A_reload_closes_the_submap_and_does_not_open_it_again()
    {
        var controller = Controller();
        controller.OnSubmapHotkey(0, Tone);

        controller.ReleaseResultHotkeys();
        AssertClosed(controller);
        controller.RestoreResultHotkeys();

        AssertClosed(controller);
        Assert.Equal(["submap: Tone: P L", "hide"], _status.Shown);
    }

    [Fact]
    public void A_command_runs_as_from_the_tray()
    {
        var controller = Controller();
        controller.OnSubmapHotkey(0, Tone);

        Assert.True(_hotkeys.Press(Key('T')));
        AssertClosed(controller);
        Assert.Equal(0, _tests); // queued, like a command chosen in the overlay
        _desktop.RunPosted();

        Assert.Equal(1, _tests);
    }

    [Fact]
    public void Slash_undo_runs_the_undo_hotkey()
    {
        var controller = Controller();
        controller.OnSubmapHotkey(0, Tone);

        Assert.True(_hotkeys.Press(Key('U')));

        AssertClosed(controller);
        Assert.Equal($"info: {Loc.Get("undo_nothing")}", _status.Last);
    }

    [Fact]
    public void An_error_while_opening_releases_everything_taken_so_far()
    {
        var controller = Controller();
        _hotkeys.ThrowOn = Key('U');

        Assert.Throws<InvalidOperationException>(() => controller.OnSubmapHotkey(0, Tone));

        AssertClosed(controller);
        Assert.Equal("hide", _status.Last);
    }
}
