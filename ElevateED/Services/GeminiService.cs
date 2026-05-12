using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ElevateED.Services
{
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private static readonly HttpClient _httpClient;

        static GeminiService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public GeminiService()
        {
            _apiKey = ConfigurationManager.AppSettings["GeminiApiKey"] ?? "";
            _baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
        }

        public async Task<GeminiResult> GeneratePodcastScriptAsync(string notesText)
        {
            var result = new GeminiResult();

            if (string.IsNullOrEmpty(_apiKey))
            {
                result.Script = GenerateFallbackScript(notesText);
                result.Success = true;
                return result;
            }

            try
            {
                var prompt = @"You are an educational podcast creator for Mpiyakhe High School students.

Convert these student notes into a friendly, engaging educational podcast script.

REQUIREMENTS:
- Make it engaging and conversational
- Sound like a friendly teacher explaining concepts
- Include an introduction that welcomes listeners
- Break down difficult concepts into simple explanations
- Include natural transitions between topics
- End with a conclusion and encouragement
- Keep it educational but not boring
- Use [PAUSE] markers for natural breaks
- Use [EMPHASIS] markers for important points
- Duration: about " + (Math.Min(notesText.Length / 100, 10)) + @" minutes when spoken

NOTES TO CONVERT:
" + TruncateText(notesText, 4000);

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 4096,
                        topP = 0.95
                    }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = _baseUrl + "?key=" + _apiKey;
                var response = await _httpClient.PostAsync(url, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    dynamic aiResponse = JsonConvert.DeserializeObject<dynamic>(responseText);

                    if (aiResponse.candidates != null && aiResponse.candidates.Count > 0)
                    {
                        var script = (string)aiResponse.candidates[0].content.parts[0].text;
                        result.Script = script ?? "Error: Empty response from AI";
                        result.Success = !string.IsNullOrEmpty(script);
                    }
                    else
                    {
                        result.ErrorMessage = "No response from AI";
                        result.Script = GenerateFallbackScript(notesText);
                        result.Success = true;
                    }
                }
                else
                {
                    result.ErrorMessage = "API Error: " + response.StatusCode;
                    result.Script = GenerateFallbackScript(notesText);
                    result.Success = true;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Script = GenerateFallbackScript(notesText);
                result.Success = true;
            }

            return result;
        }

        private string GenerateFallbackScript(string notesText)
        {
            var truncated = TruncateText(notesText, 1000);

            return @"🎙️ [PODCAST INTRO]

Welcome to ElevateED Study Podcast! I'm your host, and today we're diving into an important topic to help you ace your studies.

[PAUSE]

Let's get started with the key concepts you need to know.

" + truncated + @"

[PAUSE]

Let me break this down further. The key points to remember are:
1. Understand the core concepts first
2. Practice with examples
3. Review regularly

[EMPHASIS]
Remember, learning is a journey. Take your time and you'll master this topic!
[END EMPHASIS]

[PAUSE]

That wraps up today's podcast! Review these notes and try explaining the concepts to a friend - that's the best way to learn.

Until next time, keep studying and growing!

[PODCAST OUTRO] 🎙️";
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }

    public class GeminiResult
    {
        public bool Success { get; set; }
        public string Script { get; set; }
        public string ErrorMessage { get; set; }
    }
}