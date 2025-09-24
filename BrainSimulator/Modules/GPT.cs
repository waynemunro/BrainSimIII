using Newtonsoft.Json;
using Pluralize.NET;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using System.Threading; // added for CancellationToken & Interlocked
using System.Net; // for HttpStatusCode

namespace BrainSimulator.Modules
{
    /// <summary>
    /// Encapsulates access to the GPT API. Public API kept stable for backward compatibility.
    /// </summary>
    public static class GPT
    {
        private static readonly Pluralizer pluralizer = new();

        // Reuse a single static HttpClient (recommended practice)
        private static readonly HttpClient _httpClient = CreateHttpClient();

        // Json settings (can be extended later)
        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        // Backoff configuration
        private const int MaxRetries = 3;
        private static readonly TimeSpan[] RetryDelays = new[]
        {
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromMilliseconds(750),
            TimeSpan.FromMilliseconds(1500)
        };

        // Default request parameters (can be overridden via App.config) - kept internal
        private static double DefaultTemperature => ReadDoubleAppSetting("GPT_Temperature", 0.2, 0, 2);
        private static int DefaultMaxTokens => ReadIntAppSetting("GPT_MaxTokens", 100, 1, 4096);
        private static string DefaultModel => ConfigurationManager.AppSettings["GPT_Model"] ?? "gpt-4o";

        class CompletionResult
        {
            public class ChoiceMessage
            {
                public string role { get; set; }
                public string content { get; set; }
            }
            public class Choice
            {
                public int index { get; set; }
                public ChoiceMessage message { get; set; }
                public string finish_reason { get; set; }
                public string text { get; set; } // (legacy field for compatibility if API shape changes)
            }
            public class CompletionUsage
            {
                [JsonProperty("completion_tokens")] public int CompletionTokens { get; set; }
                [JsonProperty("prompt_tokens")] public int PromptTokens { get; set; }
                [JsonProperty("total_tokens")] public int TotalTokens { get; set; }
            }
            public class ErrorInfo
            {
                public string message { get; set; }
                public string type { get; set; }
                public object param { get; set; }
                public object code { get; set; }
            }

            public string id { get; set; }
            public string @object { get; set; }
            public long created { get; set; }
            public string model { get; set; }
            public CompletionUsage usage { get; set; }
            public List<Choice> choices { get; set; }
            public ErrorInfo error { get; set; }
        }

        // Thread-safe total token counter
        public static int totalTokensUsed = 0;

        /// <summary>
        /// Public legacy API: sends a system + user message and returns the first answer (trimmed & lowercased as before).
        /// Improvements: retries, timeout, better error parsing, static HttpClient, thread-safe token counting.
        /// </summary>
        public static async Task<string> GetGPTResult(string userText, string systemText)
        {
            // Keep legacy behavior: empty string if API key not present or malformed
            string apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith("sk"))
                return string.Empty;

            var request = BuildRequestPayload(systemText, userText);

            try
            {
                var response = await SendWithRetriesAsync(apiKey, request, CancellationToken.None).ConfigureAwait(false);
                if (response.error != null)
                {
                    return ("ERROR: " + (response.error.message ?? "Unknown error"));
                }

                if (response.usage != null)
                {
                    Interlocked.Add(ref totalTokensUsed, response.usage.TotalTokens);
                }

                string content = ExtractFirstMessageContent(response);
                if (content == null) return "ERROR: No content returned"; // Provide a consistent error string
                // Preserve legacy transformation: Trim + lower-case
                return content.Trim().ToLower();
            }
            catch (OperationCanceledException)
            {
                return "ERROR: Request canceled"; // smoother failure surface than throwing
            }
            catch (Exception ex)
            {
                // Preserve existing behavior (previously it rethrew) but with stack trace preserved.
                // For backward compatibility returning an error string instead of throwing avoids app crashes.
                return "ERROR: " + ex.Message;
            }
        }

        // Build request body (internal extensibility point)
        private static object BuildRequestPayload(string systemText, string userText) => new
        {
            temperature = DefaultTemperature,
            max_tokens = DefaultMaxTokens,
            model = DefaultModel,
            messages = new[]
            {
                new { role = "system", content = systemText },
                new { role = "user", content = userText }
            }
        };

        private static async Task<CompletionResult> SendWithRetriesAsync(string apiKey, object requestBody, CancellationToken ct)
        {
            string url = "https://api.openai.com/v1/chat/completions";
            string requestJson = JsonConvert.SerializeObject(requestBody, _jsonSettings);

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                try
                {
                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                    string responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        // Try to parse structured error
                        CompletionResult errorParsed = null;
                        try { errorParsed = JsonConvert.DeserializeObject<CompletionResult>(responseJson, _jsonSettings); } catch { /* ignore */ }

                        if (IsRetryable(response.StatusCode) && attempt < MaxRetries - 1)
                        {
                            await Task.Delay(RetryDelays[attempt], ct).ConfigureAwait(false);
                            continue;
                        }

                        if (errorParsed?.error != null)
                            return errorParsed; // contains error info

                        return new CompletionResult
                        {
                            error = new CompletionResult.ErrorInfo
                            {
                                message = $"HTTP {(int)response.StatusCode} {response.StatusCode}: {Truncate(responseJson, 300)}"
                            }
                        };
                    }

                    var result = JsonConvert.DeserializeObject<CompletionResult>(responseJson, _jsonSettings);
                    if (result == null)
                    {
                        return new CompletionResult
                        {
                            error = new CompletionResult.ErrorInfo { message = "Failed to deserialize response" }
                        };
                    }
                    return result;
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    if (attempt < MaxRetries - 1)
                    {
                        await Task.Delay(RetryDelays[attempt], ct).ConfigureAwait(false);
                        continue;
                    }
                    return new CompletionResult { error = new CompletionResult.ErrorInfo { message = "Request timeout" } };
                }
                catch (Exception ex)
                {
                    if (attempt < MaxRetries - 1)
                    {
                        await Task.Delay(RetryDelays[attempt], ct).ConfigureAwait(false);
                        continue;
                    }
                    return new CompletionResult { error = new CompletionResult.ErrorInfo { message = ex.Message } };
                }
            }
            // Should never hit here
            return new CompletionResult { error = new CompletionResult.ErrorInfo { message = "Unknown failure after retries" } };
        }

        private static bool IsRetryable(HttpStatusCode statusCode) => statusCode == HttpStatusCode.TooManyRequests ||
                                                                      statusCode == HttpStatusCode.RequestTimeout ||
                                                                      statusCode == HttpStatusCode.BadGateway ||
                                                                      statusCode == HttpStatusCode.ServiceUnavailable ||
                                                                      statusCode == HttpStatusCode.GatewayTimeout ||
                                                                      (int)statusCode == 524; // Cloudflare timeout

        private static string ExtractFirstMessageContent(CompletionResult result)
        {
            if (result?.choices == null || result.choices.Count == 0)
                return null;
            // Chat-style content
            var msgContent = result.choices[0].message?.content;
            if (!string.IsNullOrWhiteSpace(msgContent)) return msgContent;
            // Legacy 'text' fallback
            var legacy = result.choices[0].text;
            return string.IsNullOrWhiteSpace(legacy) ? null : legacy;
        }

        private static string GetApiKey()
        {
            // Priority: App.config -> Environment variable
            string apiKey = ConfigurationManager.AppSettings["APIKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            return apiKey?.Trim();
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30) // per attempt timeout
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BrainSimIII-GPTClient/1.0");
            return client;
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
            return value.Substring(0, max) + "...";
        }

        private static int ReadIntAppSetting(string key, int defaultValue, int min, int max)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (int.TryParse(raw, out var v))
            {
                if (v < min) v = min;
                if (v > max) v = max;
                return v;
            }
            return defaultValue;
        }

        private static double ReadDoubleAppSetting(string key, double defaultValue, double min, double max)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (double.TryParse(raw, out var v))
            {
                if (v < min) v = min;
                if (v > max) v = max;
                return v;
            }
            return defaultValue;
        }

        /// <summary>
        /// Encapsulates a place where you can call the pluralizer and handle known special cases.
        /// </summary>
        public static string Singularize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            List<string> ignoreWords = new() { "clothes", "wales" };
            string normalized = text.Trim().ToLower();
            if (ignoreWords.Contains(normalized)) return normalized;
            string retVal = pluralizer.Singularize(normalized) ?? normalized;
            return retVal;
        }
    }
}