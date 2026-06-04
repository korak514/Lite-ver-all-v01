using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WPF_LoginForm.Services
{
    public class GeminiApiService : IAiService
    {
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _baseUrl;

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        public GeminiApiService(string apiKey, string model = "gemini-2.0-flash")
        {
            _apiKey = apiKey;
            _model = model;
            _baseUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key=";
        }

        public async Task<string> AskAsync(List<AiMessage> messages)
        {
            string systemInstruction = null;
            var contents = new List<object>();

            foreach (var msg in messages)
            {
                if (msg.Role == "system" && systemInstruction == null)
                {
                    systemInstruction = msg.Content;
                }
                else
                {
                    string role = msg.Role == "assistant" ? "model" : "user";
                    contents.Add(new
                    {
                        role,
                        parts = new[] { new { text = msg.Content } }
                    });
                }
            }

            var requestPayload = new Dictionary<string, object>
            {
                ["contents"] = contents.ToArray(),
                ["generationConfig"] = new
                {
                    temperature = 0.2,
                    maxOutputTokens = 4096,
                    topP = 0.95
                }
            };

            if (systemInstruction != null)
            {
                requestPayload["system_instruction"] = new
                {
                    parts = new[] { new { text = systemInstruction } }
                };
            }

            string jsonBody = JsonConvert.SerializeObject(requestPayload);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(_baseUrl + _apiKey, content);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                return $"API Error: {response.StatusCode} — {error}";
            }

            string responseJson = await response.Content.ReadAsStringAsync();

            try
            {
                JObject doc = JObject.Parse(responseJson);
                JToken candidates = doc["candidates"];
                if (candidates != null && candidates.HasValues)
                {
                    JToken text = candidates[0]?["content"]?["parts"]?[0]?["text"];
                    if (text != null)
                        return text.ToString();
                }

                JToken feedback = doc["promptFeedback"];
                if (feedback != null)
                    return $"API Error: Content blocked — {feedback}";
            }
            catch (JsonException) { }

            return "Error: Unexpected API response format.";
        }
    }
}
