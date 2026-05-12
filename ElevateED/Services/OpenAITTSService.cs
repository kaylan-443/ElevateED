using System;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ElevateED.Services
{
    public class OpenAITTSService
    {
        private readonly string _apiKey;
        private static readonly HttpClient _httpClient;

        static OpenAITTSService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        }

        public OpenAITTSService()
        {
            _apiKey = ConfigurationManager.AppSettings["OpenAIApiKey"] ?? "";
        }

        public async Task<byte[]> GenerateSpeechAsync(string text, string voice = "nova", string instructions = null)
        {
            if (string.IsNullOrEmpty(_apiKey))
                return null;

            try
            {
                // Clean the text - remove podcast markers for better speech
                var cleanText = CleanTextForSpeech(text);

                var requestBody = new
                {
                    model = "gpt-4o-mini-tts",
                    voice = voice,
                    input = cleanText,
                    instructions = instructions ?? "Speak in a warm, engaging, and educational tone like a friendly teacher. Sound natural and conversational.",
                    response_format = "mp3"
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/audio/speech", content);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("OpenAI TTS Error: " + ex.Message);
            }

            return null;
        }

        private string CleanTextForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Remove podcast stage directions
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[PAUSE\]", "\n\n");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[EMPHASIS\]", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[END EMPHASIS\]", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[PODCAST INTRO\]", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[PODCAST OUTRO\]", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\(.*?music.*?\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\(Intro.*?\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\(Outro.*?\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return text.Trim();
        }
    }
}