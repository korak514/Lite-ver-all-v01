namespace WPF_LoginForm.Services
{
    public static class AiServiceFactory
    {
        public const string ProviderGemini = "gemini";
        public const string ProviderXai = "xai";
        public const string ProviderOpenRouter = "openrouter";

        public static IAiService Create(string provider, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            string lower = (provider ?? "").ToLowerInvariant();

            if (lower == ProviderXai)
                return new XaiApiService(apiKey);

            if (lower == ProviderOpenRouter)
                return new OpenRouterApiService(apiKey);

            return new GeminiApiService(apiKey);
        }
    }
}
