using Kuroko.App.Themes;

namespace Kuroko.Windows.Tests;

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
