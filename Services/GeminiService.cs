using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MeaningOverlay.Services;

/// <summary>
/// Calls the Gemini API (gemini-2.0-flash-lite — free tier: 1500 req/day)
/// with automatic retry on 429 rate-limit responses.
/// </summary>
public sealed class GeminiService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private const string GeminiBase =
        "https://generativelanguage.googleapis.com/v1beta/models/";

    // Tried in order — falls through to next if quota is exhausted
    private static readonly string[] ModelFallbacks =
    [
        "pollinations",
        "gemini-2.5-flash",    // Newest, very fast, high free quota
        "gemini-2.0-flash",    // Fast, separate free quota pool
        "gemini-1.5-flash"     // Older reliable fallback
    ];

    // Max retries per model when rate-limited (not quota-exhausted)
    private const int MaxRateRetries = 2;

    public async Task<string> ExplainAsync(string text, string apiKey,
        IProgress<string>? progress = null, bool detailedMode = false, string primaryModel = "pollinations")
    {
        // Truncate very long text to avoid token overuse
        var inputText = text.Length > 3000 ? text[..3000] + "…" : text;

        var prompt = detailedMode
            ? $"""
                You are a knowledgeable assistant. Give a thorough, detailed explanation. no comments
                
                The user copied this text:
                ---
                {inputText}
                ---
                
                Provide a detailed explanation:
                - If it's a word: give the full definition, etymology, multiple usage examples, and any related terms.
                - If it's a phrase or sentence: explain the meaning, origin/context, and nuances in depth.
                - If it's code: explain what it does step by step, mention the language, patterns used, and potential improvements.
                - If it's a math equation: solve it fully, explain each step, and describe what the concept means.
                - If it's a question: give a comprehensive, well-structured answer with examples.
                - If it's a long passage: give a detailed summary, key themes, and important takeaways.
                - Use headers or bullet points to organize if helpful.
                - Be thorough — the user wants depth, not brevity.
                """
            : $"""
                You are a helpful assistant that explains things in simple, plain language.
                no comments
                
                The user copied this text:
                ---
                {inputText}
                ---
                
                Please explain what this means in simple, clear words that anyone can understand.
                - If it's a single word, give the definition and a usage example.
                - If it's a phrase or sentence, explain what it means in everyday language.
                - If it's code then explain what it does in plain English.
                - If it's a math equation then solve it and explain what it means.
                - If it's a question, answer it in a simple and clear way.
                - If it's a long passage, summarize the key points in 3-5 bullet points.
                - Keep your response concise (max 4 sentences or 5 bullets).
                - Do NOT use jargon or technical terms unless absolutely necessary.
                - Do NOT start with "This text says..." just give the explanation directly.
                """;

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
                temperature = detailedMode ? 0.6 : 0.4,
                maxOutputTokens = detailedMode ? 4096 : 1024
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

        string? lastError = null;

        // ── Outer loop: try each model in dynamically ordered fallback list ───
        var activeFallbacks = ModelFallbacks.OrderBy(m => m.StartsWith(primaryModel) ? 0 : 1).ToArray();
        foreach (var model in activeFallbacks)
        {
            if (model == "pollinations")
            {
                try
                {
                    var pollReq = new
                    {
                        messages = new[]
                        {
                            new { role = "system", content = "You are a helpful assistant." },
                            new { role = "user", content = prompt }
                        },
                        model = "openai-fast",
                        seed = Random.Shared.Next(1, 999999999),
                        jsonMode = false,
                        @private = true,
                        stream = false
                    };
                    
                    var pollJson = JsonSerializer.Serialize(pollReq);
                    var content = new StringContent(pollJson, Encoding.UTF8, "application/json");
                    var response = await _http.PostAsync("https://text.pollinations.ai", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var resultText = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(resultText))
                            return resultText.Trim();
                    }
                }
                catch
                {
                    // Ignore and fall through to Gemini
                }
                continue;
            }

            var url = $"{GeminiBase}{model}:generateContent?key={apiKey}";

            // ── Inner loop: retry on rate-limit (429) for this model ──────────
            for (int attempt = 0; attempt <= MaxRateRetries; attempt++)
            {
                // Must create new StringContent each attempt (body stream is consumed)
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, content);

                // ── Success ───────────────────────────────────────────────────
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    using var respDoc = JsonDocument.Parse(responseBody);

                    var candidates = respDoc.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() == 0)
                        throw new Exception("Gemini returned no candidates.");

                    var resultText = candidates[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    return resultText?.Trim() ?? "No explanation returned.";
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();

                    // RESOURCE_EXHAUSTED = daily quota gone → skip to next model
                    if (IsQuotaExhausted(errorBody))
                    {
                        lastError = $"Quota exhausted for {model}";
                        break; // break inner loop → next model
                    }

                    // Rate-limited (per-minute) → wait and retry same model
                    if (attempt < MaxRateRetries)
                    {
                        var delay = ParseRetryDelay(errorBody);
                        var totalMs = (int)Math.Min(delay * 1000, 35_000);

                        var remaining = (int)Math.Ceiling(totalMs / 1000.0);
                        var elapsed = 0;
                        while (elapsed < totalMs)
                        {
                            progress?.Report(remaining.ToString());
                            await Task.Delay(Math.Min(1000, totalMs - elapsed));
                            elapsed += 1000;
                            remaining = Math.Max(0, remaining - 1);
                        }
                        progress?.Report("0");
                        continue; // retry same model
                    }

                    lastError = $"Rate limit reached on {model}";
                    break; // exhausted retries → try next model
                }

                // ── Non-429 error → don't bother trying other models ──────────
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new Exception(ExtractErrorMessage(errorBody, (int)response.StatusCode));
                }
            }
        }

        // All models exhausted
        throw new Exception(lastError ?? "All Gemini models quota-exhausted. Quotas reset daily at midnight Pacific. Try again later.");
    }

    /// <summary>
    /// Parses the retryDelay seconds from Gemini's 429 JSON body.
    /// Falls back to 30s if parsing fails.
    /// </summary>
    private static double ParseRetryDelay(string errorBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            // Gemini returns: { "error": { "details": [ { "@type": "..RetryInfo", "retryDelay": "25.03s" } ] } }
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("details", out var details))
            {
                foreach (var detail in details.EnumerateArray())
                {
                    if (detail.TryGetProperty("retryDelay", out var delayProp))
                    {
                        var delayStr = delayProp.GetString() ?? "";
                        // Format: "25.036055830s" — strip the trailing 's'
                        var match = Regex.Match(delayStr, @"([\d.]+)");
                        if (match.Success && double.TryParse(match.Groups[1].Value, out var seconds))
                            return seconds;
                    }
                }
            }
        }
        catch { }
        return 30.0; // safe default
    }

    /// <summary>
    /// Extracts a clean, user-friendly error message from Gemini's error JSON.
    /// Strips noisy googleapis.cc URLs.
    /// </summary>
    private static string ExtractErrorMessage(string errorBody, int statusCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msgProp))
            {
                var msg = msgProp.GetString() ?? "";
                // Remove the "* Quota exceeded for metric: generativelanguage.googleapis.cc/..." lines
                msg = Regex.Replace(msg, @"\*\s+Quota exceeded for metric:[^\n]*\n?", "").Trim();
                if (!string.IsNullOrWhiteSpace(msg))
                    return msg;
            }
        }
        catch { }
        return $"API error {statusCode}. Please check your API key and try again.";
    }

    /// <summary>
    /// Returns true if the 429 error is RESOURCE_EXHAUSTED (daily quota gone),
    /// false if it's just a per-minute rate limit (retryable on same model).
    /// </summary>
    private static bool IsQuotaExhausted(string errorBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                // Check status field: "RESOURCE_EXHAUSTED" = daily quota
                if (err.TryGetProperty("status", out var statusProp) &&
                    statusProp.GetString() == "RESOURCE_EXHAUSTED")
                    return true;

                // Also check message for the quota metric pattern
                if (err.TryGetProperty("message", out var msgProp))
                {
                    var msg = msgProp.GetString() ?? "";
                    if (msg.Contains("generativelanguage.googleapis.com/default-quotas") ||
                        msg.Contains("daily") ||
                        (msg.Contains("Quota exceeded") && !msg.Contains("retryDelay")))
                        return true;
                }
            }
        }
        catch { }
        return false; // treat as rate-limit by default (safer)
    }
}
