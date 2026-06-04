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
    public class XaiApiService : IAiService
    {
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _baseUrl;

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        public XaiApiService(string apiKey, string model = "grok-4.3")
        {
            string cleanKey = apiKey;
            if (cleanKey.StartsWith("XAI_API_KEY=", StringComparison.OrdinalIgnoreCase))
                cleanKey = cleanKey.Substring("XAI_API_KEY=".Length);

            _apiKey = cleanKey;
            _model = model;
            _baseUrl = "https://api.x.ai/v1/chat/completions";
        }

        public async Task<string> AskAsync(List<AiMessage> messages)
        {
            var apiMessages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray();

            var requestPayload = new
            {
                model = _model,
                messages = apiMessages,
                temperature = 0.2,
                max_tokens = 4096
            };

            string jsonBody = JsonConvert.SerializeObject(requestPayload);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl))
            {
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                request.Content = content;

                HttpResponseMessage response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    return $"API Error: {response.StatusCode} — {error}";
                }

                string responseJson = await response.Content.ReadAsStringAsync();

                try
                {
                    JObject doc = JObject.Parse(responseJson);
                    JToken text = doc["choices"]?[0]?["message"]?["content"];
                    if (text != null)
                        return text.ToString();
                }
                catch (JsonException) { }

                return "Error: Unexpected API response format.";
            }
        }
    }
}
