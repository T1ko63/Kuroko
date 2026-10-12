using System.Windows;
using System.Windows.Media;

namespace Kuroko.App.Themes;

/// <summary>Whether a font family is installed, for [appearance] font: WPF itself falls back to another font without a word.</summary>
public static class FontCheck
{
    /// <summary>True when Windows has a font of this family name (one name, not a comma-separated list).</summary>
    public static bool IsInstalled(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var typeface = new Typeface(new FontFamily(name.Trim()), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        return typeface.TryGetGlyphTypeface(out _);
    }
}
