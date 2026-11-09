
using System.Text.Json;

namespace IntelligentMonitoring.Application.Models.Logging;

public sealed class LokiLogEntry
{
    public DateTimeOffset Timestamp { get; set; }

    public string Level { get; set; } = "Unknown";

    public string? ServiceName { get; set; }

    public string? Environment { get; set; }

    public string? IntegrationType { get; set; }

    public string? EventType { get; set; }

    public string? TransactionId { get; set; }

    public string? TraceId { get; set; }

    public string? Status { get; set; }

    public string? ErrorCode { get; set; }

    public string Message { get; set; } = string.Empty;

    public double? DurationMs { get; set; }

    // Original log line returned by Loki.
    public string RawLogLine { get; set; } = string.Empty;

    // Loki labels and metadata that are not normalized properties.
    public Dictionary<string, string> SourceMetadata { get; set; } = [];

    // Integration-specific properties not covered by our standard model.
    public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = [];
}
