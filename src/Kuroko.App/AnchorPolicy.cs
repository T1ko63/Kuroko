using Kuroko.App.Overlay;
using Kuroko.Core.Config;
using Kuroko.Platform.TextIntegration;

namespace Kuroko.App;

/// <summary>
/// Where the prompt picker, the pills and the result card go for a target, following the overlay_* and result_* placement
/// settings. Holds no state besides those settings.
/// </summary>
internal sealed class AnchorPolicy
{
    public OverlayPosition Position { get; set; } = OverlayPosition.Caret;

    /// <summary>overlay_fixed_position: used when <see cref="Position"/> is <see cref="OverlayPosition.Fixed"/>.</summary>
    public CardSpot OverlaySpot { get; set; } = CardSpot.TopThird;

    /// <summary>overlay_screen_margin: used in every mode.</summary>
    public double OverlayScreenMargin { get; set; } = AppSettings.DefaultOverlayScreenMargin;

    /// <summary>result_position: where the card opens.</summary>
    public ResultPlacement ResultPlacement { get; set; } = ResultPlacement.Fixed;

    /// <summary>result_fixed_position.</summary>
    public CardSpot ResultSpot { get; set; } = CardSpot.BottomThird;

    /// <summary>result_screen_margin: distance of the card to the screen edge, in device-independent pixels.</summary>
    public double ResultScreenMargin { get; set; } = AppSettings.DefaultResultScreenMargin;

    /// <summary>pill_position: where the status pill goes (follow = where the picker opens).</summary>
    public PillPlacement PillPlacement { get; set; } = PillPlacement.Follow;

    /// <summary>pill_fixed_position: used when <see cref="PillPlacement"/> is <see cref="PillPlacement.Fixed"/>.</summary>
    public CardSpot PillSpot { get; set; } = CardSpot.TopThird;

    /// <summary>pill_screen_margin: distance of the pill to the screen edge for fixed, caret and mouse.</summary>
    public double PillScreenMargin { get; set; } = AppSettings.DefaultPillScreenMargin;

    /// <summary>Where the overlay goes for the current target (and the pills with pill_position = "follow"): a fixed place, or at the text cursor / mouse.</summary>
    public Anchor ForTarget(TargetInfo? target)
        => Position == OverlayPosition.Fixed
            ? Anchor.ResolveSpot(target, OverlaySpot, OverlayScreenMargin)
            : Anchor.Resolve(target, Position) with { MarginDip = OverlayScreenMargin };

    /// <summary>Moves a mouse-based anchor to the text cursor if UI Automation found it.</summary>
    public Anchor Refine(Anchor anchor, TargetInfo? target, FocusProbe? probe, OverlayPosition? position = null)
        => (position ?? Position) == OverlayPosition.Caret && target?.CaretScreenPos is null && probe?.Info?.CaretPoint is { } caret
            ? Anchor.AtCaret(caret)
            : anchor;

    /// <summary>Where the card opens: a fixed place, like the picker (follow), at the text cursor, or at the mouse.</summary>
    public Anchor ForResult(TargetInfo? target, FocusProbe? probe, Anchor pickerAnchor) => ResultPlacement switch
    {
        ResultPlacement.Fixed => Anchor.ResolveSpot(target, ResultSpot, ResultScreenMargin),
        ResultPlacement.Caret => Refine(Anchor.Resolve(target, OverlayPosition.Caret), target, probe, OverlayPosition.Caret) with { MarginDip = ResultScreenMargin },
        ResultPlacement.Mouse => Anchor.Resolve(target, OverlayPosition.Mouse) with { MarginDip = ResultScreenMargin },
        _ => Refine(pickerAnchor, target, probe) with { MarginDip = ResultScreenMargin },
    };

    /// <summary>
    /// Where a pill goes, given where the picker opened (or would open) for this target: there (follow), at the text cursor,
    /// at the mouse, at a fixed place (with pill_screen_margin) or where the card would appear. A card prompt's pill does
    /// not come here: it sits where its card will appear (<see cref="ForResult"/>), because it turns into the card.
    /// </summary>
    public Anchor ForPill(Anchor pickerAnchor, TargetInfo? target, FocusProbe? probe = null) => PillPlacement switch
    {
        PillPlacement.Fixed => Anchor.ResolveSpot(target, PillSpot, PillScreenMargin),
        PillPlacement.Caret => Refine(Anchor.Resolve(target, OverlayPosition.Caret), target, probe, OverlayPosition.Caret) with { MarginDip = PillScreenMargin },
        PillPlacement.Mouse => Anchor.Resolve(target, OverlayPosition.Mouse) with { MarginDip = PillScreenMargin },
        PillPlacement.Card => ForResult(target, probe, pickerAnchor),
        _ => pickerAnchor,
    };
}
