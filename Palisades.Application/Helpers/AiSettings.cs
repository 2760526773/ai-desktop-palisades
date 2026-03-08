using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Palisades.Helpers
{
    internal class AiSettings
    {
        public string Provider { get; set; } = "openai";
        public Dictionary<string, ProviderSettings> Providers { get; set; } = CreateDefaultProviders();

        public List<string> Extensions { get; set; } = new() { ".lnk", ".url", ".exe", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".pdf", ".txt", ".md", ".mp3", ".wav", ".flac", ".mp4", ".mkv", ".avi", ".zip", ".7z", ".rar" };

        public static AiSettings Load()
        {
            string file = Path.Combine(PDirectory.GetAppDirectory(), "ai-settings.json");
            if (!File.Exists(file))
            {
                AiSettings settings = new();
                settings.Save();
                return settings;
            }

            try
            {
                string json = File.ReadAllText(file);
                AiSettings? loaded = JsonSerializer.Deserialize<AiSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (loaded == null)
                {
                    return new AiSettings();
                }

                loaded.Providers ??= new Dictionary<string, ProviderSettings>(StringComparer.OrdinalIgnoreCase);
                loaded.MergeMissingDefaultProviders();
                loaded.EnsureRecommendedModels();
                if (string.IsNullOrWhiteSpace(loaded.Provider) || !loaded.Providers.ContainsKey(loaded.Provider))
                {
                    loaded.Provider = "openai";
                }
                return loaded;
            }
            catch
            {
                return new AiSettings();
            }
        }

        public void Save()
        {
            EnsureRecommendedModels();
            string dir = PDirectory.GetAppDirectory();
            PDirectory.EnsureExists(dir);
            string file = Path.Combine(dir, "ai-settings.json");
            File.WriteAllText(file, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void MergeMissingDefaultProviders()
        {
            foreach (var pair in CreateDefaultProviders())
            {
                if (!Providers.ContainsKey(pair.Key))
                {
                    Providers[pair.Key] = pair.Value;
                }
            }
        }

        private void EnsureRecommendedModels()
        {
            Dictionary<string, ProviderSettings> defaults = CreateDefaultProviders();
            foreach (var pair in Providers)
            {
                pair.Value.RecommendedModels ??= new List<string>();
                if (defaults.TryGetValue(pair.Key, out ProviderSettings? @default))
                {
                    foreach (string model in @default.RecommendedModels)
                    {
                        if (!pair.Value.RecommendedModels.Any(existing => string.Equals(existing, model, StringComparison.OrdinalIgnoreCase)))
                        {
                            pair.Value.RecommendedModels.Add(model);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(pair.Value.Model) && !pair.Value.RecommendedModels.Any(existing => string.Equals(existing, pair.Value.Model, StringComparison.OrdinalIgnoreCase)))
                {
                    pair.Value.RecommendedModels.Insert(0, pair.Value.Model);
                }
            }
        }

        private static Dictionary<string, ProviderSettings> CreateDefaultProviders()
        {
            return new Dictionary<string, ProviderSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["openai"] = new ProviderSettings
                {
                    Type = "openai",
                    BaseUrl = "https://api.openai.com/v1/chat/completions",
                    Model = "gpt-4o-mini",
                    EnvKey = "OPENAI_API_KEY",
                    RecommendedModels = new() { "gpt-4o-mini", "gpt-4.1-mini", "gpt-4.1", "o4-mini" }
                },
                ["kimi"] = new ProviderSettings
                {
                    Type = "openai",
                    BaseUrl = "https://api.moonshot.cn/v1/chat/completions",
                    Model = "kimi-latest",
                    EnvKey = "KIMI_API_KEY",
                    RecommendedModels = new() { "kimi-latest", "moonshot-v1-8k", "moonshot-v1-32k", "moonshot-v1-128k" }
                },
                ["doubao"] = new ProviderSettings
                {
                    Type = "openai",
                    BaseUrl = "https://ark.cn-beijing.volces.com/api/v3/chat/completions",
                    Model = "doubao-pro-32k",
                    EnvKey = "ARK_API_KEY",
                    RecommendedModels = new() { "doubao-pro-32k", "doubao-lite-32k", "doubao-seed-1-6-thinking" }
                },
                ["deepseek"] = new ProviderSettings
                {
                    Type = "openai",
                    BaseUrl = "https://api.deepseek.com/chat/completions",
                    Model = "deepseek-chat",
                    EnvKey = "DEEPSEEK_API_KEY",
                    RecommendedModels = new() { "deepseek-chat", "deepseek-reasoner" }
                },
                ["gemini"] = new ProviderSettings
                {
                    Type = "gemini",
                    BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models",
                    Model = "gemini-2.5-flash",
                    EnvKey = "GEMINI_API_KEY",
                    RecommendedModels = new() { "gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.0-flash" }
                },
                ["qwen"] = new ProviderSettings
                {
                    Type = "openai",
                    BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
                    Model = "qwen-plus",
                    EnvKey = "DASHSCOPE_API_KEY",
                    RecommendedModels = new() { "qwen-plus", "qwen-turbo", "qwen-max", "qwen-long" }
                },
                ["groq"] = new ProviderSettings
                {
                    Type = "openai",
                    BaseUrl = "https://api.groq.com/openai/v1/chat/completions",
                    Model = "openai/gpt-oss-20b",
                    EnvKey = "GROQ_API_KEY",
                    RecommendedModels = new() { "openai/gpt-oss-20b", "llama-3.3-70b-versatile", "deepseek-r1-distill-llama-70b" }
                },
                ["grok"] = new ProviderSettings
                {
                    Type = "openai",
                    BaseUrl = "https://api.x.ai/v1/chat/completions",
                    Model = "grok-3-fast",
                    EnvKey = "XAI_API_KEY",
                    RecommendedModels = new() { "grok-3-fast", "grok-3", "grok-3-mini" }
                },
                ["openrouter"] = new ProviderSettings
                {
                    Type = "openai",
                    BaseUrl = "https://openrouter.ai/api/v1/chat/completions",
                    Model = "openai/gpt-4.1-mini",
                    EnvKey = "OPENROUTER_API_KEY",
                    RecommendedModels = new() { "openai/gpt-4.1-mini", "google/gemini-2.5-flash", "anthropic/claude-3.7-sonnet", "deepseek/deepseek-chat-v3-0324" }
                },
                ["mistral"] = new ProviderSettings
                {
                    Type = "openai",
                    BaseUrl = "https://api.mistral.ai/v1/chat/completions",
                    Model = "mistral-small-latest",
                    EnvKey = "MISTRAL_API_KEY",
                    RecommendedModels = new() { "mistral-small-latest", "mistral-medium-latest", "pixtral-large-latest" }
                },
                ["custom"] = new ProviderSettings
                {
                    Type = "openai",
                    BaseUrl = "https://api.openai.com/v1/chat/completions",
                    Model = "gpt-4o-mini",
                    EnvKey = "OPENAI_API_KEY",
                    RecommendedModels = new() { "gpt-4o-mini" }
                },
            };
        }
    }

    internal class ProviderSettings
    {
        public string Type { get; set; } = "openai";
        public string BaseUrl { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string EnvKey { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public List<string> RecommendedModels { get; set; } = new();
    }
}



