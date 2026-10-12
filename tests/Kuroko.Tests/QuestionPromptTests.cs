using Kuroko.Core.Prompts;
using Kuroko.Core.Providers;

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

public class QuestionAnswerLengthTests
{
    private static string Answer() => FakeHandler.Events(
        ("message_start", "{\"type\":\"message_start\"}"),
        ("content_block_delta", "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"OK\"}}"),
        ("message_delta", "{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}"),
        ("message_stop", "{\"type\":\"message_stop\"}"));

    private static async Task<int> MaxTokensFor(PromptDefinition prompt, string input)
    {
        var http = new FakeHandler().Respond(FakeHandler.Sse(Answer()));
        var settings = ProviderTestHelpers.Settings(ProviderType.Anthropic, "https://api.anthropic.test");
        var registry = new ProviderRegistry(new Dictionary<string, ProviderSettings> { [settings.Name] = settings }, settings.Name, new HttpClient(http));
        await new ProviderPromptRunner(() => registry).RunAsync(prompt, input, CancellationToken.None);
        return (int)Assert.Single(http.Calls).Json["max_tokens"]!;
    }

    [Fact]
    public async Task A_question_about_a_short_selection_gets_the_generous_output_limit()
    {
        var (prompt, input) = QuestionPrompt.Build("Erklär das ausführlich.", "Kurzer Satz.");

        Assert.True(prompt.OpenEndedAnswer);
        Assert.Equal(PromptBuilder.EstimateMaxOutputTokens(PromptMode.Instruction, input.Length, false), await MaxTokensFor(prompt, input));
    }

    [Fact]
    public async Task A_normal_transform_prompt_keeps_its_limit()
    {
        var prompt = new PromptDefinition("Fix", "Fix it.");

        Assert.Equal(PromptBuilder.EstimateMaxOutputTokens(PromptMode.Transform, 12, false), await MaxTokensFor(prompt, "Kurzer Satz."));
    }

    [Fact]
    public void The_role_does_not_ask_for_brevity()
        => Assert.DoesNotContain("concise", QuestionPrompt.Role, StringComparison.OrdinalIgnoreCase);
}
