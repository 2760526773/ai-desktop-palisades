using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Palisades.Helpers
{
    internal sealed class AiRunInfo
    {
        public bool AttemptedAi { get; set; }
        public bool UsedAiResult { get; set; }
        public string Provider { get; set; } = string.Empty;
        public int ParsedItems { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    internal static class AiClassifier
    {
        private static readonly string[] Categories = { "游戏", "影音", "娱乐", "文档", "开发", "社交", "工具", "系统", "其他" };

        private static readonly Dictionary<string, string[]> Keywords = new()
        {
            ["游戏"] = new[] { "steam", "epic", "game", "lol", "dota", "genshin", "minecraft", "battle" },
            ["影音"] = new[] { "video", "music", "vlc", "spotify", "player", "mpv", "bilibili" },
            ["娱乐"] = new[] { "douyin", "tiktok", "live", "comic", "娱乐", "抖音" },
            ["文档"] = new[] { "word", "excel", "ppt", "pdf", "docs", "文档" },
            ["开发"] = new[] { "code", "studio", "terminal", "git", "docker", "python", "node", "dev" },
            ["社交"] = new[] { "wechat", "qq", "discord", "slack", "telegram", "teams", "飞书", "微信" },
            ["工具"] = new[] { "zip", "rar", "7z", "chrome", "edge", "firefox", "tool" },
            ["系统"] = new[] { "control", "settings", "cmd", "powershell", "regedit", "taskmgr", "系统" },
        };

        private static readonly Dictionary<string, string> ExtensionCategory = new(StringComparer.OrdinalIgnoreCase)
        {
            [".doc"] = "文档", [".docx"] = "文档", [".ppt"] = "文档", [".pptx"] = "文档", [".xls"] = "文档", [".xlsx"] = "文档", [".pdf"] = "文档", [".txt"] = "文档", [".md"] = "文档",
            [".mp3"] = "影音", [".wav"] = "影音", [".flac"] = "影音", [".mp4"] = "影音", [".mkv"] = "影音", [".avi"] = "影音",
            [".ps1"] = "开发", [".py"] = "开发", [".js"] = "开发", [".ts"] = "开发",
            [".zip"] = "工具", [".7z"] = "工具", [".rar"] = "工具", [".lnk"] = "工具", [".url"] = "工具",
        };

        internal static AiRunInfo LastRunInfo { get; private set; } = new();

        internal static Dictionary<string, string> Classify(List<DesktopCandidate> candidates, AiSettings settings)
        {
            Dictionary<string, string> output = new(StringComparer.OrdinalIgnoreCase);
            foreach (DesktopCandidate c in candidates)
            {
                output[c.Path] = Heuristic(c.Name, c.Extension, c.IsDirectory);
            }

            LastRunInfo = new AiRunInfo
            {
                Provider = settings.Provider,
                Message = "未尝试 AI，已使用本地规则。"
            };

            try
            {
                Dictionary<string, string> ai = ClassifyWithAi(candidates, settings, out string providerName, out int batchCount);
                int usedCount = 0;
                foreach (var item in ai)
                {
                    if (!output.ContainsKey(item.Key))
                    {
                        continue;
                    }

                    if (Categories.Any(c => c == item.Value))
                    {
                        output[item.Key] = item.Value;
                        usedCount++;
                    }
                }

                LastRunInfo = new AiRunInfo
                {
                    AttemptedAi = true,
                    UsedAiResult = usedCount > 0,
                    Provider = providerName,
                    ParsedItems = usedCount,
                    Message = usedCount > 0
                        ? $"AI 分类成功（{providerName}），分 {batchCount} 批处理，应用 {usedCount} 条分类。"
                        : $"AI 已调用（{providerName}），但未返回可用分类，已回退本地规则。"
                };
            }
            catch (Exception ex)
            {
                LastRunInfo = new AiRunInfo
                {
                    AttemptedAi = true,
                    UsedAiResult = false,
                    Provider = settings.Provider,
                    ParsedItems = 0,
                    Message = $"AI 调用失败：{ex.Message}（已回退本地规则）"
                };
            }

            return output;
        }

        private static string Heuristic(string name, string ext, bool isDirectory)
        {
            if (isDirectory)
            {
                string text = name.ToLowerInvariant();
                if (text.Contains("game") || text.Contains("游戏")) return "游戏";
                if (text.Contains("video") || text.Contains("music") || text.Contains("影音")) return "影音";
                if (text.Contains("doc") || text.Contains("资料") || text.Contains("文档")) return "文档";
                if (text.Contains("code") || text.Contains("dev") || text.Contains("项目") || text.Contains("开发")) return "开发";
                return "其他";
            }

            if (ExtensionCategory.TryGetValue(ext, out string? category))
            {
                return category;
            }

            string lower = name.ToLowerInvariant();
            foreach (var pair in Keywords)
            {
                foreach (string keyword in pair.Value)
                {
                    if (lower.Contains(keyword))
                    {
                        return pair.Key;
                    }
                }
            }

            return "其他";
        }

        private static Dictionary<string, string> ClassifyWithAi(List<DesktopCandidate> candidates, AiSettings settings, out string providerName, out int batchCount)
        {
            providerName = settings.Provider;
            batchCount = 0;

            if (!settings.Providers.TryGetValue(settings.Provider, out ProviderSettings? provider))
            {
                throw new InvalidOperationException($"未找到提供商配置：{settings.Provider}");
            }

            string apiKey = provider.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(provider.EnvKey))
            {
                apiKey = Environment.GetEnvironmentVariable(provider.EnvKey) ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException($"{providerName} 未配置 API Key");
            }
            if (string.IsNullOrWhiteSpace(provider.BaseUrl) || string.IsNullOrWhiteSpace(provider.Model))
            {
                throw new InvalidOperationException($"{providerName} 的 BaseUrl/Model 为空");
            }

            int batchSize = DecideBatchSize(provider.Model, candidates.Count);
            var batches = SplitCandidates(candidates, batchSize);
            batchCount = batches.Count;

            Dictionary<string, string> merged = new(StringComparer.OrdinalIgnoreCase);
            foreach (List<DesktopCandidate> batch in batches)
            {
                string prompt = BuildPrompt(batch);
                string content = string.Equals(provider.Type, "gemini", StringComparison.OrdinalIgnoreCase)
                    ? CallGemini(provider.BaseUrl, provider.Model, apiKey, prompt)
                    : CallOpenAiCompatible(provider.BaseUrl, provider.Model, apiKey, prompt);

                Dictionary<string, string> one = ParseAiResult(content, batch);
                foreach (var kv in one)
                {
                    merged[kv.Key] = kv.Value;
                }
            }

            return merged;
        }

        private static int DecideBatchSize(string model, int total)
        {
            int tokenK = 0;
            Match m = Regex.Match(model ?? string.Empty, "(\\d+)\\s*[kK]");
            if (m.Success)
            {
                int.TryParse(m.Groups[1].Value, out tokenK);
            }

            int size = tokenK switch
            {
                <= 0 => 45,
                <= 8 => 20,
                <= 16 => 35,
                <= 32 => 70,
                <= 64 => 110,
                _ => 160,
            };

            return Math.Clamp(Math.Min(size, total), 8, 200);
        }

        private static List<List<DesktopCandidate>> SplitCandidates(List<DesktopCandidate> candidates, int batchSize)
        {
            List<List<DesktopCandidate>> batches = new();
            for (int i = 0; i < candidates.Count; i += batchSize)
            {
                int take = Math.Min(batchSize, candidates.Count - i);
                batches.Add(candidates.GetRange(i, take));
            }
            return batches;
        }

        private static string BuildPrompt(List<DesktopCandidate> candidates)
        {
            var compact = candidates.Select(c => new
            {
                id = c.Id,
                name = c.DisplayName,
                ext = c.Extension,
                is_dir = c.IsDirectory
            });

            return "你是 Windows 桌面项目分类器。只允许分类为：游戏,影音,娱乐,文档,开发,社交,工具,系统,其他。"
                + "请只输出 JSON，不要解释。首选输出对象：{\"id\":\"分类\"}。"
                + "也可输出数组：[{\"id\":1,\"category\":\"分类\"}]。"
                + "待分类项目："
                + JsonSerializer.Serialize(compact);
        }

        private static HttpClient CreateHttpClient(string? bearerToken = null)
        {
            HttpClientHandler handler = new()
            {
                UseProxy = true,
                Proxy = WebRequest.DefaultWebProxy,
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
            };

            HttpClient http = new(handler) { Timeout = TimeSpan.FromSeconds(30) };
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }
            return http;
        }

        private static string CallOpenAiCompatible(string baseUrl, string model, string apiKey, string prompt)
        {
            using HttpClient http = CreateHttpClient(apiKey);
            object payload = new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = "你负责做桌面程序类型分类。" },
                    new { role = "user", content = prompt },
                },
                temperature = 0.1,
            };
            string json = JsonSerializer.Serialize(payload);
            using StringContent body = new(json, Encoding.UTF8, "application/json");

            using HttpResponseMessage resp = http.PostAsync(baseUrl, body).GetAwaiter().GetResult();
            string response = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(response, 220)}");
            }

            using JsonDocument doc = JsonDocument.Parse(response);
            if (!doc.RootElement.TryGetProperty("choices", out JsonElement choices) || choices.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("响应缺少 choices 字段");
            }

            string? text = choices[0].GetProperty("message").GetProperty("content").GetString();
            return text ?? "{}";
        }

        private static string CallGemini(string baseUrl, string model, string apiKey, string prompt)
        {
            string endpoint = $"{baseUrl.TrimEnd('/')}/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";
            using HttpClient http = CreateHttpClient();

            object payload = new
            {
                contents = new object[] { new { parts = new object[] { new { text = prompt } } } },
                generationConfig = new { temperature = 0.1 }
            };
            string json = JsonSerializer.Serialize(payload);
            using StringContent body = new(json, Encoding.UTF8, "application/json");

            using HttpResponseMessage resp = http.PostAsync(endpoint, body).GetAwaiter().GetResult();
            string response = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(response, 220)}");
            }

            using JsonDocument doc = JsonDocument.Parse(response);
            if (!doc.RootElement.TryGetProperty("candidates", out JsonElement candidates))
            {
                throw new InvalidOperationException("Gemini 响应缺少 candidates 字段");
            }

            StringBuilder builder = new();
            foreach (JsonElement candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out JsonElement content))
                {
                    continue;
                }
                if (!content.TryGetProperty("parts", out JsonElement parts))
                {
                    continue;
                }
                foreach (JsonElement part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out JsonElement text))
                    {
                        builder.AppendLine(text.GetString());
                    }
                }
            }
            return builder.ToString();
        }

        private static Dictionary<string, string> ParseAiResult(string content, List<DesktopCandidate> candidates)
        {
            string text = ExtractJson(content);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> keyToPath = BuildKeyToPathIndex(candidates);

            try
            {
                using JsonDocument doc = JsonDocument.Parse(text);
                JsonElement root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty prop in root.EnumerateObject())
                    {
                        string rawKey = prop.Name;
                        string rawCategory = prop.Value.GetString() ?? string.Empty;
                        TryAddMapped(result, keyToPath, rawKey, rawCategory);
                    }

                    if (result.Count == 0)
                    {
                        ParseObjectArrayLike(result, keyToPath, root);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    ParseArrayItems(result, keyToPath, root);
                }
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return result;
        }

        private static void ParseObjectArrayLike(Dictionary<string, string> result, Dictionary<string, string> keyToPath, JsonElement root)
        {
            if (root.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
            {
                ParseArrayItems(result, keyToPath, items);
            }
            else if (root.TryGetProperty("result", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
            {
                ParseArrayItems(result, keyToPath, arr);
            }
        }

        private static void ParseArrayItems(Dictionary<string, string> result, Dictionary<string, string> keyToPath, JsonElement array)
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string rawKey = ReadFirstString(item, "id", "path", "file", "filepath", "name", "display_name");
                string rawCategory = ReadFirstString(item, "category", "type", "class", "分类", "类别");
                TryAddMapped(result, keyToPath, rawKey, rawCategory);
            }
        }

        private static string ReadFirstString(JsonElement obj, params string[] names)
        {
            foreach (string n in names)
            {
                if (!obj.TryGetProperty(n, out JsonElement v))
                {
                    continue;
                }

                if (v.ValueKind == JsonValueKind.String)
                {
                    return v.GetString() ?? string.Empty;
                }

                if (v.ValueKind == JsonValueKind.Number)
                {
                    return v.ToString();
                }
            }
            return string.Empty;
        }

        private static Dictionary<string, string> BuildKeyToPathIndex(List<DesktopCandidate> candidates)
        {
            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);

            foreach (DesktopCandidate c in candidates)
            {
                AddIfMissing(map, NormalizeKey(c.Id.ToString()), c.Path);
                AddIfMissing(map, NormalizeKey(c.Path), c.Path);
                AddIfMissing(map, NormalizeKey(c.DisplayName), c.Path);
                AddIfMissing(map, NormalizeKey(c.Name), c.Path);
                AddIfMissing(map, NormalizeKey(Path.GetFileName(c.Path)), c.Path);
                AddIfMissing(map, NormalizeKey(Path.GetFileNameWithoutExtension(c.Path)), c.Path);
            }

            return map;
        }

        private static void AddIfMissing(Dictionary<string, string> map, string key, string path)
        {
            if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
            {
                map[key] = path;
            }
        }

        private static void TryAddMapped(Dictionary<string, string> result, Dictionary<string, string> keyToPath, string rawKey, string rawCategory)
        {
            if (string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(rawCategory))
            {
                return;
            }

            string normalized = NormalizeCategory(rawCategory);
            if (!Categories.Contains(normalized))
            {
                return;
            }

            string key = NormalizeKey(rawKey);
            if (keyToPath.TryGetValue(key, out string? path))
            {
                result[path] = normalized;
            }
        }

        private static string ExtractJson(string content)
        {
            string text = (content ?? string.Empty).Trim();
            if (text.StartsWith("```"))
            {
                text = Regex.Replace(text, "^```(?:json)?", "", RegexOptions.IgnoreCase).Trim();
                text = Regex.Replace(text, "```$", "").Trim();
            }

            int startObj = text.IndexOf('{');
            int endObj = text.LastIndexOf('}');
            int startArr = text.IndexOf('[');
            int endArr = text.LastIndexOf(']');

            int objLen = (startObj >= 0 && endObj > startObj) ? endObj - startObj + 1 : -1;
            int arrLen = (startArr >= 0 && endArr > startArr) ? endArr - startArr + 1 : -1;

            if (objLen <= 0 && arrLen <= 0)
            {
                return string.Empty;
            }

            return arrLen > objLen ? text.Substring(startArr, arrLen) : text.Substring(startObj, objLen);
        }

        private static string NormalizeKey(string value)
        {
            string v = (value ?? string.Empty).Trim().Trim('"', '\'', '`');
            v = v.Replace('/', '\\');
            return v.ToLowerInvariant();
        }

        private static string NormalizeCategory(string raw)
        {
            string v = (raw ?? string.Empty).Trim().Replace("\"", string.Empty);
            if (Categories.Contains(v))
            {
                return v;
            }

            string lower = v.ToLowerInvariant();
            if (lower.Contains("game") || lower.Contains("游戏")) return "游戏";
            if (lower.Contains("video") || lower.Contains("media") || lower.Contains("影音") || lower.Contains("music")) return "影音";
            if (lower.Contains("entertain") || lower.Contains("娱乐")) return "娱乐";
            if (lower.Contains("doc") || lower.Contains("office") || lower.Contains("pdf") || lower.Contains("文档")) return "文档";
            if (lower.Contains("dev") || lower.Contains("code") || lower.Contains("开发") || lower.Contains("program")) return "开发";
            if (lower.Contains("social") || lower.Contains("chat") || lower.Contains("社交")) return "社交";
            if (lower.Contains("tool") || lower.Contains("util") || lower.Contains("工具")) return "工具";
            if (lower.Contains("system") || lower.Contains("driver") || lower.Contains("系统")) return "系统";
            return "其他";
        }

        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
            {
                return text;
            }
            return text[..maxLen] + "...";
        }
    }

    internal class DesktopCandidate
    {
        public int Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
    }
}
