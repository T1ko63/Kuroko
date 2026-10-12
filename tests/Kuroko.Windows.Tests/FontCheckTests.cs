using Kuroko.App.Themes;

namespace Kuroko.Windows.Tests;

/// <summary>
/// The first font lookup loads the system font cache, which takes the CPU for a moment. These tests run on their own, so the
/// timing-sensitive clipboard tests (TextAccessServiceTests: a 20 ms follow-up within a 100 ms window) never share the CPU with it.
/// </summary>
[CollectionDefinition(nameof(FontLookup), DisableParallelization = true)]
public sealed class FontLookup;

[Collection(nameof(FontLookup))]
public class FontCheckTests
{
    [Theory]
    [InlineData("Segoe UI", true)]
    [InlineData(" Arial ", true)]
    [InlineData("No Such Font 4711", false)]
    [InlineData("", false)]
    public void Installed_fonts_are_found_and_unknown_names_are_not(string name, bool expected)
        => Assert.Equal(expected, FontCheck.IsInstalled(name));
}
