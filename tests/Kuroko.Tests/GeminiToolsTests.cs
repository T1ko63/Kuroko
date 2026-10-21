using System.Net;
using Kuroko.Core.Config;
using Kuroko.Core.Prompts;
using Kuroko.Core.Providers;
using static Kuroko.Tests.FakeHandler;
using static Kuroko.Tests.ProviderTestHelpers;

namespace Kuroko.Tests;

/// <summary>gtools in prompts.toml: read by the loader, sent by Gemini only, carried to the fallback provider.</summary>
public class GeminiToolsTests
{
    private const string GeminiBase = "https://generativelanguage.test/v1beta";

    private static PromptsLoadResult Parse(string gtools)
        => PromptsLoader.Parse($"[[prompt]]\nname = \"A\"\nprompt = \"x\"\n{gtools}\n");

    private static string GeminiAnswer(string text)
        => Events((null, $"{{\"candidates\":[{{\"content\":{{\"parts\":[{{\"text\":\"{text}\"}}]}},\"finishReason\":\"STOP\"}}]}}"));

    private static string AnthropicAnswer(string text) => Events(
        ("content_block_delta", $"{{\"type\":\"content_block_delta\",\"delta\":{{\"type\":\"text_delta\",\"text\":\"{text}\"}}}}"),
        ("message_stop", "{\"type\":\"message_stop\"}"));

    private static async Task<SeenRequest> GeminiCallAsync(IReadOnlyList<GeminiTool>? tools)
    {
        var http = new FakeHandler().Respond(Sse(GeminiAnswer("ok")));
        var provider = new GeminiProvider(Settings(ProviderType.Gemini, GeminiBase), new HttpClient(http));
        await CollectAsync(provider, new LlmRequest("model-x", "SYS", "USER", 512, GeminiTools: tools));
        return Assert.Single(http.Calls);
    }

    [Fact]
    public void Missing_gtools_means_no_tools()
    {
        var result = Parse(string.Empty);
        Assert.Empty(result.Issues);
        Assert.Empty(Assert.Single(result.Prompts!).GeminiTools);
    }

    [Fact]
    public void Names_are_case_insensitive_trimmed_and_counted_once()
    {
        var result = Parse("gtools = [\"URLCONTEXT\", \" googlesearch \", \"urlContext\", \"googleSearch\"]");
        Assert.Empty(result.Issues);
        Assert.Equal([GeminiTool.UrlContext, GeminiTool.GoogleSearch], Assert.Single(result.Prompts!).GeminiTools);
    }

    [Fact]
    public void Empty_list_means_no_tools()
    {
        var result = Parse("gtools = []");
        Assert.Empty(result.Issues);
        Assert.Empty(Assert.Single(result.Prompts!).GeminiTools);
    }

    [Theory]
    [InlineData("gtools = [\"googleSearch\", \"codeExecution\"]", "codeExecution")]
    [InlineData("gtools = [\"googleSearch\", 3]", "'3'")]
    public void Unknown_entry_is_a_warning_and_skipped(string gtools, string named)
    {
        var result = Parse(gtools);
        Assert.True(result.Ok);
        var issue = Assert.Single(result.Issues);
        Assert.False(issue.IsError);
        Assert.Contains(named, issue.Message);
        Assert.Equal(4, issue.Line);
        Assert.Equal([GeminiTool.GoogleSearch], Assert.Single(result.Prompts!).GeminiTools);
    }

    [Theory]
    [InlineData("gtools = \"googleSearch\"")]
    [InlineData("gtools = true")]
    public void Value_that_is_not_a_list_is_a_warning_and_ignored(string gtools)
    {
        var result = Parse(gtools);
        Assert.True(result.Ok);
        var issue = Assert.Single(result.Issues);
        Assert.False(issue.IsError);
        Assert.Contains("must be a list", issue.Message);
        Assert.Empty(Assert.Single(result.Prompts!).GeminiTools);
    }

    [Fact]
    public async Task Gemini_without_tools_leaves_the_tools_key_out()
    {
        Assert.False((await GeminiCallAsync(null)).Json.AsObject().ContainsKey("tools"));
        Assert.False((await GeminiCallAsync([])).Json.AsObject().ContainsKey("tools"));
    }

    [Fact]
    public async Task Gemini_sends_each_requested_tool_once()
    {
        var call = await GeminiCallAsync([GeminiTool.GoogleSearch, GeminiTool.UrlContext, GeminiTool.GoogleSearch]);
        Assert.Equal("[{\"googleSearch\":{}},{\"urlContext\":{}}]", call.Json["tools"]!.ToJsonString());
    }

    public static TheoryData<ProviderType> OtherProviders => [ProviderType.OpenAi, ProviderType.Anthropic, ProviderType.OpenAiCompatible];

    [Theory]
    [MemberData(nameof(OtherProviders))]
    public async Task Other_providers_send_the_same_bytes_with_or_without_tools(ProviderType type)
    {
        async Task<string> BodyAsync(IReadOnlyList<GeminiTool>? tools)
        {
            // Rejected at once (400, no retry), so the body is all that matters.
            var http = new FakeHandler().Respond(Json(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"nope\"}}"));
            var settings = Settings(type, "https://other.test/v1", reasoning: "default");
            ILlmProvider provider = type switch
            {
                ProviderType.OpenAi => new OpenAiProvider(settings, new HttpClient(http)),
                ProviderType.Anthropic => new AnthropicProvider(settings, new HttpClient(http)),
                _ => new OpenAiCompatibleProvider(settings, new HttpClient(http)),
            };
            await FailureAsync(provider, new LlmRequest("model-x", "SYS", "USER", 512, GeminiTools: tools));
            return Assert.Single(http.Calls).Body;
        }

        var without = await BodyAsync(null);
        Assert.Equal(without, await BodyAsync([GeminiTool.GoogleSearch, GeminiTool.UrlContext]));
        Assert.DoesNotContain("googleSearch", without);
    }

    [Fact]
    public void Question_prompt_uses_google_search_with_and_without_selection()
    {
        Assert.Equal([GeminiTool.GoogleSearch], QuestionPrompt.Build("Wo liegt Sachalin?", null).Prompt.GeminiTools);
        Assert.Equal([GeminiTool.GoogleSearch], QuestionPrompt.Build("Stimmt das?", "Sachalin liegt im Mittelmeer.").Prompt.GeminiTools);
    }

    private static ProviderRegistry Registry(FakeHandler http, ProviderType mainType)
    {
        var settings = new Dictionary<string, ProviderSettings>
        {
            ["main"] = new($"main-{Guid.NewGuid():N}", mainType, "https://main.test", "main-model", "key", TimeSpan.FromSeconds(5), FallbackProvider: "backup"),
            ["backup"] = new($"backup-{Guid.NewGuid():N}", ProviderType.Gemini, "https://backup.test", "backup-model", "key", TimeSpan.FromSeconds(5)),
        };
        return new ProviderRegistry(settings, "main", new HttpClient(http));
    }

    [Theory]
    [InlineData(PromptMode.Transform)]
    [InlineData(PromptMode.Universal)]
    public async Task Runner_passes_the_prompt_tools_to_gemini(PromptMode mode)
    {
        var http = new FakeHandler().Respond(Sse(GeminiAnswer("ok")));
        using var registry = Registry(http, ProviderType.Gemini);
        var prompt = new PromptDefinition("P", "Check it.", mode) { GeminiTools = [GeminiTool.UrlContext] };

        await new ProviderPromptRunner(() => registry).RunAsync(prompt, "text", CancellationToken.None);

        Assert.Equal("[{\"urlContext\":{}}]", Assert.Single(http.Calls).Json["tools"]!.ToJsonString());
    }

    [Fact]
    public async Task Runner_without_tools_sends_none_to_gemini()
    {
        var http = new FakeHandler().Respond(Sse(GeminiAnswer("ok")));
        using var registry = Registry(http, ProviderType.Gemini);

        await new ProviderPromptRunner(() => registry).RunAsync(new PromptDefinition("P", "Fix it."), "text", CancellationToken.None);

        Assert.False(Assert.Single(http.Calls).Json.AsObject().ContainsKey("tools"));
    }

    [Fact]
    public async Task Tools_travel_with_the_request_to_a_gemini_fallback()
    {
        var http = new FakeHandler()
            .Respond(Json(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"nope\"}}"))
            .Respond(Sse(GeminiAnswer("ok")));
        using var registry = Registry(http, ProviderType.Anthropic);
        var prompt = new PromptDefinition("P", "Check it.") { GeminiTools = [GeminiTool.GoogleSearch] };

        Assert.Equal("ok", await new ProviderPromptRunner(() => registry).RunAsync(prompt, "text", CancellationToken.None));

        Assert.False(http.Calls[0].Json.AsObject().ContainsKey("tools"));
        Assert.Equal("backup.test", http.Calls[^1].Uri.Host);
        Assert.Equal("[{\"googleSearch\":{}}]", http.Calls[^1].Json["tools"]!.ToJsonString());
    }

    private static AppSettings Config(string? defaultProvider, params (string Name, ProviderType Type)[] providers)
        => AppSettings.Fallback with
        {
            DefaultProvider = defaultProvider,
            Providers = providers.ToDictionary(
                p => p.Name,
                p => new ProviderSettings(p.Name, p.Type, "https://x.test", "m", "key", TimeSpan.FromSeconds(30)),
                StringComparer.OrdinalIgnoreCase),
        };

    private static List<ConfigIssue> Validate(AppSettings settings, PromptDefinition[] prompts)
        => ConfigValidator.Validate(settings, prompts, KeybindSettings.Default).Where(i => i.File == PromptsLoader.FileName).ToList();

    private static readonly PromptDefinition WithTools = new("Check", "x") { GeminiTools = [GeminiTool.GoogleSearch] };

    [Fact]
    public void Gtools_on_a_prompt_with_a_non_gemini_provider_is_a_warning()
    {
        var settings = Config("g", ("g", ProviderType.Gemini), ("o", ProviderType.OpenAi));
        var issue = Assert.Single(Validate(settings, [WithTools with { Provider = "o" }]));
        Assert.False(issue.IsError);
        Assert.Contains("gtools", issue.Message);
        Assert.Contains("'o'", issue.Message);
    }

    [Fact]
    public void Gtools_with_a_non_gemini_default_provider_is_a_warning()
    {
        var settings = Config("a", ("a", ProviderType.Anthropic));
        var issue = Assert.Single(Validate(settings, [WithTools]));
        Assert.False(issue.IsError);
        Assert.Contains("gtools", issue.Message);
    }

    [Fact]
    public void Gtools_with_gemini_or_without_tools_gives_no_warning()
    {
        var settings = Config("g", ("g", ProviderType.Gemini), ("o", ProviderType.OpenAi));
        Assert.Empty(Validate(settings, [WithTools, WithTools with { Name = "Other", Provider = "G" }]));
        Assert.Empty(Validate(settings, [new PromptDefinition("Plain", "x", Provider: "o")]));
    }
}
