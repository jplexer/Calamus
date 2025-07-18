using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mscc.GenerativeAI;

namespace Calamus.Bot.Services;

public class GeminiService(IOptions<GeminiConfig> config)
{
    private GoogleAI _ai = new (apiKey: config.Value.ApiKey);

    public async Task<string> ReplyToPromptAsync(string prompt)
    {
        try
        {
            var model = _ai.GenerativeModel(model: "gemini-2.5-flash");
            var response = await model.GenerateContent(prompt);
            return $"{response.Text}";
        }
        catch (Exception ex)
        {
            return "Gemini failed to generate.";
        }
    }
}