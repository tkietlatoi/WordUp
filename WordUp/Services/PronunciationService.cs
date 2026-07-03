using System.Net.Http;
using System.Text.Json;

namespace WordUp.Services;

public sealed class PronunciationService
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://api.dictionaryapi.dev/api/v2/entries/en/")
    };

    public async Task<string> GetIpaAsync(string word, CancellationToken cancellationToken = default)
    {
        var normalizedWord = NormalizeWord(word);
        if (string.IsNullOrWhiteSpace(normalizedWord))
        {
            return "";
        }

        try
        {
            using var response = await HttpClient.GetAsync(Uri.EscapeDataString(normalizedWord), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return "";
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ReadIpa(document.RootElement);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return "";
        }
    }

    private static string NormalizeWord(string word)
    {
        var trimmed = word.Trim();
        var firstToken = trimmed.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstToken?.Trim(',', '.', ';', ':', '!', '?', '"', '\'') ?? "";
    }

    private static string ReadIpa(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        foreach (var entry in root.EnumerateArray())
        {
            if (TryGetText(entry, "phonetic", out var phonetic))
            {
                return phonetic;
            }

            if (!entry.TryGetProperty("phonetics", out var phonetics)
                || phonetics.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in phonetics.EnumerateArray())
            {
                if (TryGetText(item, "text", out var text))
                {
                    return text;
                }
            }
        }

        return "";
    }

    private static bool TryGetText(JsonElement element, string propertyName, out string value)
    {
        value = "";
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim() ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }
}
