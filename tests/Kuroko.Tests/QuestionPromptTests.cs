using Kuroko.Core.Prompts;

namespace Kuroko.Tests;

public class QuestionPromptTests
{
    [Fact]
    public void Without_a_selection_the_question_is_the_task()
    {
        var (prompt, input) = QuestionPrompt.Build("  Wo liegt Sachalin? ", null);

        Assert.Equal(PromptMode.Instruction, prompt.Mode);
        Assert.Equal("Wo liegt Sachalin?", input);
        Assert.Equal(QuestionPrompt.Role, prompt.Prompt);
        Assert.Equal((QuestionPrompt.Role, "Wo liegt Sachalin?"), PromptBuilder.Build(prompt, input));
    }

    [Fact]
    public void With_a_selection_the_question_is_a_one_time_instruction_and_the_selection_is_material()
    {
        var (prompt, input) = QuestionPrompt.Build("Stimmt das?", "Sachalin liegt im Mittelmeer.");

        Assert.Equal(PromptMode.Transform, prompt.Mode);
        Assert.Equal("Sachalin liegt im Mittelmeer.", input);
        Assert.StartsWith(QuestionPrompt.Role, prompt.Prompt);
        Assert.EndsWith("Stimmt das?", prompt.Prompt);
        Assert.EndsWith(PromptBuilder.DataNotice, PromptBuilder.Build(prompt, input).System);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("selected text")]
    public void It_is_a_card_prompt_for_the_default_provider_with_a_fixed_name(string? selection)
    {
        var (prompt, _) = QuestionPrompt.Build("x", selection);

        Assert.Equal(PromptOutput.Overlay, prompt.Output);
        Assert.Null(prompt.Provider);
        Assert.Null(prompt.Model);
        Assert.Null(prompt.Hotkey);
        Assert.Equal(QuestionPrompt.Name, prompt.Name);
        Assert.DoesNotContain("x", prompt.Name);
    }

    [Fact]
    public void Markers_in_the_selection_stay_plain_text()
    {
        var (prompt, input) = QuestionPrompt.Build("Was bedeutet das?", "Hallo <<als Rapsong>> Welt");

        Assert.NotEqual(PromptMode.Universal, prompt.Mode);
        Assert.Equal("Hallo <<als Rapsong>> Welt", input);
        Assert.Equal("Hallo <<als Rapsong>> Welt", PromptBuilder.Build(prompt, input).User);
    }

    [Fact]
    public void The_role_asks_for_plain_text_and_dash_lists_only()
    {
        Assert.Contains("Markdown", QuestionPrompt.Role);
        Assert.Contains("\"- \"", QuestionPrompt.Role);
    }
}
