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

CRITICAL RULES:
- Write in PLAIN CONVERSATIONAL TEXT only
- DO NOT use any formatting markers like: [PAUSE], [EMPHASIS], **bold**, *italic*
- DO NOT include stage directions like (Intro Music), (Outro Music fades)
- DO NOT include speaker labels like Teacher: or Host:
- Write EXACTLY as you would speak naturally
- Use natural line breaks between paragraphs
- Sound like a friendly teacher talking directly to students
- Make it educational and engaging
- Keep the language warm and conversational

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

            return @"Hey Mpiyakhe High students! Welcome to your study podcast.

Today we're breaking down some important concepts to help you prepare for your exams. Let's dive right in.

" + truncated + @"

Let me highlight the key takeaways from this material. Understanding these core concepts is essential for your success. Take time to review each point and make sure you can explain them in your own words.

Practice makes perfect. Try applying these concepts to real-world examples and past exam questions. That's the best way to cement your understanding.

You've got this! Keep studying, stay focused, and remember that every bit of effort brings you closer to your goals.

This has been your ElevateED study podcast. Happy studying!";
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