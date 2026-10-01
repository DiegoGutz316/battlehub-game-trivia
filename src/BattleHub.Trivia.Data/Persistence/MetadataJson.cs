using System.Text.Json;
using BattleHub.Trivia.Domain.Entities;

namespace BattleHub.Trivia.Data.Persistence;

internal static class MetadataJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(TriviaMetadata metadata) =>
        JsonSerializer.Serialize(metadata, Options);

    public static TriviaMetadata Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new TriviaMetadata();

        return JsonSerializer.Deserialize<TriviaMetadata>(json, Options) ?? new TriviaMetadata();
    }
}
