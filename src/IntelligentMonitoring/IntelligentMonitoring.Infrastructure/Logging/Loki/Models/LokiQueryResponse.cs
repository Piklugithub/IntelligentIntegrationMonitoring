using System.Text.Json.Serialization;

namespace IntelligentMonitoring.Infrastructure.Logging.Loki.Models;

public sealed class LokiQueryResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public LokiQueryData? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class LokiQueryData
{
    [JsonPropertyName("resultType")]
    public string ResultType { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public List<LokiStreamResult> Result { get; set; } = [];
}

public sealed class LokiStreamResult
{
    [JsonPropertyName("stream")]
    public Dictionary<string, string> Stream { get; set; } = [];

    [JsonPropertyName("values")]
    public List<string[]> Values { get; set; } = [];
}