using Kuroko.App.Overlay;
using Kuroko.Core.Config;
using Kuroko.Core.Diagnostics;
using Kuroko.Core.Hotkeys;
using Kuroko.Platform.Hotkeys;

namespace Kuroko.App;

/// <summary>
/// An open submap: its label in the pill, its keys and Esc registered as temporary hotkeys without modifiers, and a timer.
/// A listed key, Esc, the timeout, a click on the pill or anything else the controller starts closes it, and closing
/// always releases every key it took. Keys that are not listed are never taken, so they type as usual (there is no
/// keyboard hook). Without a registry nothing is registered.
/// </summary>
internal sealed class SubmapSession(IStatusView status, IDesktop desktop, IHotkeyRegistry? hotkeys)
{
    private readonly List<TemporaryHotkey> _keys = [];
    private TemporaryHotkey? _esc;
    private IDisposable? _timer;
    private int _generation;

    /// <summary>The submap that is open now; null when none is.</summary>
    public SubmapDefinition? Current { get; private set; }

    public bool IsOpen => Current is not null;

    /// <summary>Opens <paramref name="submap"/>; <paramref name="chosen"/> runs after it closed again for a listed key.</summary>
    public void Open(SubmapDefinition submap, Anchor anchor, Action<SubmapKey, long> chosen)
    {
        Close();
        Current = submap;
        var generation = ++_generation;
        try
        {
            // The pill first: it replaces an error pill whose copy hotkey is then released before the keys are taken.
            status.ShowSubmap(submap.Label, anchor);

            _esc = new TemporaryHotkey(hotkeys, $"Submap '{submap.Name}': Esc could not be registered");
            _esc.Register(TemporaryHotkey.Escape, _ =>
            {
                AppLog.Info($"submap '{submap.Name}': Esc");
                Close();
            });

            var held = 0;
            foreach (var key in submap.Keys)
            {
                // A key another app holds globally is missing from this submap; the others still work.
                var slot = new TemporaryHotkey(hotkeys, $"Submap '{submap.Name}': key {key.Key} is left out, it could not be registered");
                _keys.Add(slot);
                var captured = key;
                if (slot.Register(new HotkeyGesture(HotkeyModifiers.None, key.VirtualKey), timestamp => Choose(captured, timestamp, chosen))) held++;
            }

            AppLog.Info($"submap '{submap.Name}' open: {held} of {submap.Keys.Count} keys, {submap.TimeoutMs} ms");
            _timer = desktop.StartTimer(TimeSpan.FromMilliseconds(submap.TimeoutMs), () =>
            {
                if (generation != _generation) return; // a tick that was already queued when this submap closed
                AppLog.Info($"submap '{submap.Name}': timeout");
                Close();
            });
        }
        catch
        {
            Close();
            throw;
        }
    }

    private void Choose(SubmapKey key, long timestamp, Action<SubmapKey, long> chosen)
    {
        if (!IsOpen) return;
        Close();
        chosen(key, timestamp);
    }

    /// <summary>Releases every key and Esc, stops the timer and hides the pill. Does nothing when no submap is open.</summary>
    /// <param name="hidePill">False when something else is about to replace the pill anyway.</param>
    public void Close(bool hidePill = true)
    {
        if (Current is null) return;
        Current = null;
        _generation++;
        _timer?.Dispose();
        _timer = null;
        _esc?.Release();
        _esc = null;
        foreach (var key in _keys) key.Release();
        _keys.Clear();
        if (hidePill) status.HideStatus();
    }
}
