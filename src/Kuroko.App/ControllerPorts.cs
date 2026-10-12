using System.Windows.Media;
using Kuroko.App.Overlay;
using Kuroko.Core.Prompts;
using Kuroko.Platform.Native;
using Kuroko.Platform.TextIntegration;
using Kuroko.Platform.Windowing;

namespace Kuroko.App;

/// <summary>The prompt picker as <see cref="AppController"/> uses it. <see cref="OverlayWindow"/> is the real one; tests use a fake.</summary>
public interface IOverlayView
{
    event Action<PromptDefinition>? PromptChosen;

    event Action<AppCommand>? CommandChosen;

    event Action<string>? QuestionAsked;

    event Action? Cancelled;

    event Action? Dismissed;

    bool IsShown { get; }

    void SetLayout(double width, double minHeight, double maxHeight, double fontSize);

    void SetPlaceholder(string text);

    /// <summary>[mods] of settings.toml, so prompt hotkeys are listed with their variables filled in.</summary>
    void SetHotkeyMods(IReadOnlyDictionary<string, Kuroko.Core.Hotkeys.HotkeyModifiers> mods);

    void Present(IReadOnlyList<PromptDefinition> prompts, IReadOnlyList<AppCommand> commands);

    void ShowAnchored(Anchor anchor);

    void ActivateForInput();

    void HideOverlay();
}

/// <summary>The status pill and result card as <see cref="AppController"/> uses them. <see cref="StatusWindow"/> is the real one.</summary>
public interface IStatusView
{
    event Action? CancelRequested;

    event Action? ResultClosed;

    event Action? ActionEnded;

    bool IsActionShown { get; }

    double ResultFontSize { get; set; }

    void SetResultSize(double width, double minHeight, double maxHeight);

    void SetPillLayout(PillLayout layout);

    void ShowProgress(string text, Anchor anchor);

    void UpdateProgress(string text, Anchor anchor);

    void ShowError(string text, Anchor anchor);

    void ShowInfo(string text, Anchor anchor, bool longer = false);

    void ShowErrorWithAction(string text, Anchor anchor);

    /// <summary>The label of an open submap; stays until <see cref="HideStatus"/>, a click raises <see cref="CancelRequested"/>.</summary>
    void ShowSubmap(string text, Anchor anchor);

    void HideStatus();

    void BeginResult(string label, Anchor anchor);

    void AppendResult(string piece);

    void EndResult();

    /// <summary>Runs <paramref name="action"/> on the window's thread: directly if already there, otherwise queued.</summary>
    void RunOnUi(Action action);
}

/// <summary>
/// The parts of Windows <see cref="AppController"/> needs besides reading and writing text: the window under the hotkey,
/// giving the focus back, the timer resolution, the foreground watch and the render loop (for latency logging only).
/// </summary>
public interface IDesktop
{
    /// <summary>The window (and field) that has the focus right now; null when there is none.</summary>
    TargetInfo? CaptureTarget();

    /// <summary>Gives the focus back to <paramref name="target"/>; false when it did not become the foreground window.</summary>
    Task<bool> RestoreFocusAsync(TargetInfo target);

    void SetHighResolutionTimer(bool on);

    /// <summary>Calls <paramref name="changed"/> when another window becomes the foreground window; null when that cannot be watched.</summary>
    IDisposable? WatchForeground(nint window, Action changed);

    /// <summary>Calls <paramref name="action"/> once the next frame has been rendered.</summary>
    void AfterNextFrame(Action action);

    /// <summary>Runs <paramref name="action"/> later on the UI thread, after the current work (e.g. closing the overlay) has finished.</summary>
    void Post(Action action);

    /// <summary>Calls <paramref name="elapsed"/> once on the UI thread after <paramref name="delay"/>; disposing the result stops it.</summary>
    IDisposable StartTimer(TimeSpan delay, Action elapsed);
}

/// <summary>The real <see cref="IDesktop"/>.</summary>
public sealed class Win32Desktop : IDesktop
{
    public TargetInfo? CaptureTarget() => TargetInfo.Capture();

    public Task<bool> RestoreFocusAsync(TargetInfo target) => WindowHelper.RestoreFocusAsync(target);

    public void SetHighResolutionTimer(bool on) => NativeTimer.SetHighResolution(on);

    public IDisposable? WatchForeground(nint window, Action changed) => ForegroundWatcher.Start(window, changed);

    public void AfterNextFrame(Action action)
    {
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            CompositionTarget.Rendering -= handler;
            action();
        };
        CompositionTarget.Rendering += handler;
    }

    public void Post(Action action) => System.Windows.Application.Current.Dispatcher.BeginInvoke(action);

    public IDisposable StartTimer(TimeSpan delay, Action elapsed)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            elapsed();
        };
        timer.Start();
        return new StopTimer(timer);
    }

    private sealed class StopTimer(System.Windows.Threading.DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
