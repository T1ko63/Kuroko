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

/// <summary>
/// Size and font of the status pill and how long an info and an error pill stay (pill_min_width, pill_max_width,
/// pill_min_height, pill_font_size, pill_duration, pill_error_duration).
/// </summary>
/// <param name="ErrorDuration">Null: an error stays until it is clicked or Esc is pressed (pill_error_duration = 0).</param>
public sealed record PillLayout(double MinWidth, double MaxWidth, double MinHeight, double FontSize, TimeSpan InfoDuration, TimeSpan? ErrorDuration)
{
    /// <summary>pill_error_duration in seconds as <see cref="ErrorDuration"/>: 0 becomes null (stays).</summary>
    public static TimeSpan? ErrorDurationOf(double seconds) => seconds <= 0 ? null : TimeSpan.FromSeconds(seconds);
}
