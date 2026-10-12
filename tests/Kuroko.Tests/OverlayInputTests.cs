using Kuroko.Core.Search;

namespace Kuroko.Tests;

public class OverlayInputTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("corr", "corr")]
    [InlineData("  Über setzen  ", "Über setzen")]
    [InlineData("a/b", "a/b")]
    [InlineData(null, "")]
    public void Anything_without_a_leading_slash_is_a_prompt_search(string? input, string query)
        => Assert.Equal(new OverlayInput(OverlayInputKind.Search, query), OverlayInput.Parse(input));

    [Theory]
    [InlineData("/", "", "")]
    [InlineData("/copy", "copy", "")]
    [InlineData("  /copy  ", "copy", "")]
    [InlineData("/ copy", "copy", "")]
    [InlineData("/blur acrylic", "blur", "acrylic")]
    [InlineData("/settings set  theme dark ", "settings", "set  theme dark")]
    public void A_leading_slash_is_a_command_with_an_optional_value(string input, string name, string value)
        => Assert.Equal(new OverlayInput(OverlayInputKind.Command, name, value), OverlayInput.Parse(input));

    [Theory]
    [InlineData("//", "")]
    [InlineData("//   ", "")]
    [InlineData("// Wo liegt Sachalin? ", "Wo liegt Sachalin?")]
    [InlineData("  //x", "x")]
    [InlineData("///x", "/x")]
    public void Two_leading_slashes_make_the_rest_a_question(string input, string question)
        => Assert.Equal(new OverlayInput(OverlayInputKind.Question, question), OverlayInput.Parse(input));
}
