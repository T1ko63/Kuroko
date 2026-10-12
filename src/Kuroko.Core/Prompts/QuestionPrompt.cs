using Kuroko.Core.Localization;

namespace Kuroko.Core.Prompts;

/// <summary>
/// A one-off question typed after "//" in the overlay. The answer is always shown in the result card; nothing is replaced.
/// It has its own fixed role and nothing to do with the universal prompt. No provider or model is set, so the default
/// provider answers.
/// </summary>
public static class QuestionPrompt
{
    /// <summary>The role. The card shows plain text and turns lines starting with "- " into a list; nothing else is rendered.</summary>
    public const string Role =
        "You answer the user's question or carry out their instruction. Your answer is shown in a small card that displays "
        + "plain text only: do not use Markdown (no headings, bold, italics, tables, links or code fences). For a list, put each "
        + "item on its own line starting with \"- \". Be concise. Answer in the language of the question unless it asks for another one.";

    /// <summary>The fixed name shown in the progress pill and written to the log; the question itself never is.</summary>
    public static string Name => Loc.Get("question_name");

    /// <summary>
    /// The prompt and the text to send. With selected text the question is a one-time instruction about that text, which is
    /// sent as material (transform: the data notice keeps instructions inside it from being followed). Without a selection the
    /// question itself is the task. Never <see cref="PromptMode.Universal"/>, so markers in the text stay plain text.
    /// </summary>
    public static (PromptDefinition Prompt, string Input) Build(string question, string? selectedText)
    {
        question = question.Trim();
        if (string.IsNullOrEmpty(selectedText))
        {
            return (new PromptDefinition(Name, Role, PromptMode.Instruction, Output: PromptOutput.Overlay), question);
        }

        var prompt = $"{Role}\n\n"
            + "The question or instruction refers to the text of the user message:\n"
            + $"{question}";
        return (new PromptDefinition(Name, prompt, PromptMode.Transform, Output: PromptOutput.Overlay), selectedText);
    }
}
