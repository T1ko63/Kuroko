namespace Kuroko.App;

/// <summary>result_position: a fixed place, the place of the prompt picker (follow), the text cursor or the mouse.</summary>
public enum ResultPlacement
{
    Fixed,
    Follow,
    Caret,
    Mouse,
}

/// <summary>pill_position: where the prompt picker opens (follow), the text cursor, the mouse, a fixed place, or where the card appears.</summary>
public enum PillPlacement
{
    Follow,
    Caret,
    Mouse,
    Fixed,
    Card,
}

/// <summary>Size and font of the status pill and how long an info pill stays (pill_min_width, pill_max_width, pill_min_height, pill_font_size, pill_duration).</summary>
public sealed record PillLayout(double MinWidth, double MaxWidth, double MinHeight, double FontSize, TimeSpan InfoDuration);
