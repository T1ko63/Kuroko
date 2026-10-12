namespace Kuroko.Core.Config;

public enum BlurMode
{
    Acrylic,
    Mica,
    MicaAlt,
    Blur,
    None,
}

/// <summary>How the selected row is marked (selection_style): a filled area, or only a frame.</summary>
public enum SelectionStyle
{
    Fill,
    Border,
}

/// <summary>
/// The colours of one theme, all opaque (the selected row's opacity is selection_opacity). Border and separator are
/// optional (border_color, separator_color): when missing they are derived from the foreground.
/// </summary>
public sealed record ThemeColors(Rgba Background, Rgba Foreground, Rgba Selection, Rgba? Border = null, Rgba? Separator = null);

/// <summary>The [appearance] block of settings.toml, validated, with defaults for everything that is missing or invalid.</summary>
public sealed record AppearanceSettings(
    int Transparency, int Radius, BlurMode Blur, ThemeColors Dark, ThemeColors Light, bool Border = true, bool CustomCorners = false)
{
    /// <summary>Grey text in the empty search field of the prompt picker; "" shows none.</summary>
    public string Placeholder { get; init; } = DefaultPlaceholder;

    /// <summary>How the selected row is marked (selection_style).</summary>
    public SelectionStyle SelectionStyle { get; init; } = DefaultSelectionStyle;

    /// <summary>Opacity of the selected row in percent (selection_opacity), for both styles; hover has half of it.</summary>
    public int SelectionOpacity { get; init; } = DefaultSelectionOpacity;

    public const SelectionStyle DefaultSelectionStyle = SelectionStyle.Fill;

    /// <summary>25 % is alpha 0x40, the alpha of the old default selection colours (#40FFFFFF, #40000000).</summary>
    public const int DefaultSelectionOpacity = 25;

    /// <summary>The line under the search field (separator).</summary>
    public bool Separator { get; init; } = DefaultSeparator;

    /// <summary>Width of the window border in px (border_width); <see cref="BorderThickness"/> is what is drawn.</summary>
    public int BorderWidth { get; init; } = DefaultLineWidth;

    /// <summary>Height of the line under the search field in px (separator_width); <see cref="SeparatorThickness"/> is what is drawn.</summary>
    public int SeparatorWidth { get; init; } = DefaultLineWidth;

    /// <summary>Font of the overlay, the status pill, the result card and the API key window; a comma-separated list falls back in order.</summary>
    public string Font { get; init; } = DefaultFont;

    /// <summary>The border as drawn: border = false means 0, whatever border_width says.</summary>
    public int BorderThickness => Border ? BorderWidth : 0;

    /// <summary>The separator as drawn: separator = false means 0.</summary>
    public int SeparatorThickness => Separator ? SeparatorWidth : 0;

    public const string DefaultPlaceholder = "Kuroko";
    public const bool DefaultSeparator = true;
    public const int DefaultLineWidth = 1;
    public const int MaxLineWidth = 4;
    public const string DefaultFont = "Segoe UI Variable Text, Segoe UI";
    public const int DefaultTransparency = 50;
    public const int DefaultRadius = 10;
    public const BlurMode DefaultBlur = BlurMode.Acrylic;
    public const int MaxRadius = 100;
    public const bool DefaultBorder = true;

    /// <summary>
    /// custom_corners: the corner radius is meant to be applied exactly, also on a system material (Acrylic, Mica). Windows itself only
    /// offers 0, 4 and 8 px there; the Windhawk mod "Kuroko Blur" (corners) makes the window manager use the real radius.
    /// </summary>
    public const bool DefaultCustomCorners = false;

    public static readonly ThemeColors DefaultDark = new(
        Rgba.Opaque(0x00, 0x00, 0x00), Rgba.Opaque(0xF0, 0xF0, 0xF0), Rgba.Opaque(0xFF, 0xFF, 0xFF));

    public static readonly ThemeColors DefaultLight = new(
        Rgba.Opaque(0xFF, 0xFF, 0xFF), Rgba.Opaque(0x1A, 0x1A, 0x1A), Rgba.Opaque(0x00, 0x00, 0x00));

    public static AppearanceSettings Default { get; } = new(DefaultTransparency, DefaultRadius, DefaultBlur, DefaultDark, DefaultLight, DefaultBorder);

    /// <summary>How much of the window surface is visible: 100 - transparency, in percent.</summary>
    public int OpacityPercent => 100 - Transparency;

    /// <summary>Alpha (0..255) of the window surface colour.</summary>
    public byte SurfaceAlpha => OpacityToAlpha(OpacityPercent);

    /// <summary>Corner radius of the highlighted row: a bit smaller than the window's.</summary>
    public int RowRadius => Math.Min(MaxRowRadius, (int)Math.Round(Radius * 0.6, MidpointRounding.AwayFromZero));

    /// <summary>A row is 38 px high; a larger corner radius would only make it a pill, so the rows stop at half of that.</summary>
    public const int MaxRowRadius = 19;

    public ThemeColors For(bool dark) => dark ? Dark : Light;

    /// <summary>
    /// The corner size a window on a Windows 11 system material (Acrylic, Mica) really gets: DWM only offers 0, 4 and
    /// 8 px, so the configured radius snaps to the nearest of these.
    /// </summary>
    public static int NativeCornerRadius(int radius) => radius switch
    {
        < 2 => 0,
        < 6 => 4,
        _ => 8,
    };

    public static byte OpacityToAlpha(int percent)
        => (byte)Math.Round(Math.Clamp(percent, 0, 100) * 255 / 100.0, MidpointRounding.AwayFromZero);
}

/// <summary>
/// The colours the UI needs, all derived from the configured ones. The text parts only differ in the opacity of the foreground.
/// A row is marked by its area (RowSelected, RowHover) or its frame (RowSelectedEdge, RowHoverEdge), whichever selection_style
/// asks for; the other one is transparent, so the frame is always there and the text never moves.
/// </summary>
public sealed record AppearancePalette(
    Rgba Surface, Rgba Edge, Rgba Text, Rgba Muted, Rgba Subtle, Rgba Placeholder, Rgba Separator,
    Rgba RowSelected, Rgba RowHover, Rgba ScrollThumb, Rgba Progress, Rgba Caret, Rgba TextSelection,
    bool BackgroundIsDark, Rgba RowSelectedEdge = default, Rgba RowHoverEdge = default)
{
    // Opacity of the foreground colour for each secondary use.
    public const double MutedOpacity = 0.62;
    public const double SubtleOpacity = 0.42;
    public const double PlaceholderOpacity = 0.5;
    public const double SeparatorOpacity = 0.12;
    public const double ScrollThumbOpacity = 0.45;
    public const double ProgressOpacity = 0.7;
    public const double TextSelectionOpacity = 0.3;
    public const double EdgeOpacity = 0.16;

    public static AppearancePalette Derive(AppearanceSettings appearance, bool dark)
    {
        var colors = appearance.For(dark);
        var fg = colors.Foreground;
        var bg = colors.Background;
        var selected = colors.Selection.WithAlpha(AppearanceSettings.OpacityToAlpha(appearance.SelectionOpacity));
        var hover = selected.Scaled(0.5);
        var clear = selected.WithAlpha(0);
        var framed = appearance.SelectionStyle == SelectionStyle.Border;
        return new AppearancePalette(
            Surface: bg.WithAlpha(appearance.SurfaceAlpha),
            Edge: colors.Border ?? fg.Scaled(EdgeOpacity).Over(bg),
            Text: fg,
            Muted: fg.Scaled(MutedOpacity),
            Subtle: fg.Scaled(SubtleOpacity),
            Placeholder: fg.Scaled(PlaceholderOpacity),
            Separator: colors.Separator ?? fg.Scaled(SeparatorOpacity),
            RowSelected: framed ? clear : selected,
            RowHover: framed ? clear : hover,
            ScrollThumb: fg.Scaled(ScrollThumbOpacity),
            Progress: fg.Scaled(ProgressOpacity),
            Caret: fg,
            TextSelection: fg.Scaled(TextSelectionOpacity),
            BackgroundIsDark: bg.Luminance < 0.5,
            RowSelectedEdge: framed ? selected : clear,
            RowHoverEdge: framed ? hover : clear);
    }
}
