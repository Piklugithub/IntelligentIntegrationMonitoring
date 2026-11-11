
using System.Globalization;
using System.Text.Json;
using IntelligentMonitoring.Application.Abstractions.Logging;
using IntelligentMonitoring.Application.Models.Logging;
using IntelligentMonitoring.Infrastructure.Logging.Loki.Models;
using Microsoft.Extensions.Logging;

namespace IntelligentMonitoring.Infrastructure.Logging.Loki;

public sealed class LokiLogRepository(HttpClient httpClient, ILogger<LokiLogRepository> _logger)
    : ILokiLogRepository
{
    private const int DefaultLimit = 1000;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<LokiLogEntry>> SearchByTransactionIdAsync(
        string transactionId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
     {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

        var query =
            $"{{job=\"integration-logs\"}} | json | transactionId=\"{EscapeLogQlString(transactionId)}\"";

        return QueryLogsAsync(
                query,
                startTime,
                endTime,
                DefaultLimit,
                cancellationToken);
    }

    public Task<IReadOnlyList<LokiLogEntry>> SearchByTraceIdAsync(
        string traceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);

        var query =
            $"{{job=\"integration-logs\"}} | json | traceId=\"{EscapeLogQlString(traceId)}\"";

        return QueryLogsAsync(
            query,
            startTime,
            endTime,
            DefaultLimit,
            cancellationToken);
    }

    private async Task<IReadOnlyList<LokiLogEntry>> QueryLogsAsync(
    string query,
    DateTimeOffset startTime,
    DateTimeOffset endTime,
    int limit,
    CancellationToken cancellationToken)
    {
        if (startTime >= endTime)
        {
            throw new ArgumentException(
                "Start time must be earlier than end time.");
        }

        // Loki expects timestamps in Unix nanoseconds.
        var startNanoseconds =
            startTime.ToUnixTimeMilliseconds() * 1_000_000;

        var endNanoseconds =
            endTime.ToUnixTimeMilliseconds() * 1_000_000;

        var queryString =
            $"query={Uri.EscapeDataString(query)}" +
            $"&start={startNanoseconds}" +
            $"&end={endNanoseconds}" +
            $"&limit={limit}" +
            "&direction=backward";

        _logger.LogInformation(
            """
            Sending Loki query.
            Query: {Query}
            Start UTC: {Start}
            End UTC: {End}
            Start Nanoseconds: {StartNanoseconds}
            End Nanoseconds: {EndNanoseconds}
            """,
            query,
            startTime,
            endTime,
            startNanoseconds,
            endNanoseconds);

        using var response = await httpClient.GetAsync(
            $"loki/api/v1/query_range?{queryString}",
            cancellationToken);

        var responseContent =
            await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation(
            "Loki HTTP response status: {StatusCode}",
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Loki request failed. Response: {Response}",
                responseContent);

            throw new HttpRequestException(
                $"Loki request failed. Status: {(int)response.StatusCode}. " +
                $"Response: {responseContent}");
        }

        var lokiResponse =
            JsonSerializer.Deserialize<LokiQueryResponse>(
                responseContent,
                JsonOptions);

        if (lokiResponse is null)
        {
            throw new InvalidOperationException(
                "Unable to deserialize Loki response.");
        }

        if (!string.Equals(
                lokiResponse.Status,
                "success",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Loki query failed: {lokiResponse.Error ?? "Unknown error"}");
        }

        var entries = new List<LokiLogEntry>();

        if (lokiResponse.Data?.Result is null)
        {
            _logger.LogWarning(
                "Loki returned no result streams for query: {Query}",
                query);

            return entries;
        }

        foreach (var stream in lokiResponse.Data.Result)
        {
            foreach (var value in stream.Values)
            {
                if (value.Length < 2)
                    continue;

                entries.Add(
                    MapToLogEntry(
                        value[0],
                        value[1],
                        stream.Stream));
            }
        }

        _logger.LogInformation(
            "Loki query completed. Returned {LogCount} log entries.",
            entries.Count);

        return entries;
    }


    public Task<IReadOnlyList<LokiLogEntry>> SearchAsync(
        LokiLogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StartTime >= request.EndTime)
        {
            throw new ArgumentException(
                "Start time must be earlier than end time.");
        }

        // Avoid unrestricted Loki queries.
        var hasSearchFilter =
            !string.IsNullOrWhiteSpace(request.TransactionId) ||
            !string.IsNullOrWhiteSpace(request.TraceId) ||
            !string.IsNullOrWhiteSpace(request.ServiceName) ||
            !string.IsNullOrWhiteSpace(request.Level) ||
            !string.IsNullOrWhiteSpace(request.IntegrationType);

        if (!hasSearchFilter)
        {
            throw new ArgumentException(
                "At least one search filter must be provided.");
        }

        var filters = new List<string>();

        AddFilter(filters, "transactionId", request.TransactionId);
        AddFilter(filters, "traceId", request.TraceId);
        AddFilter(filters, "serviceName", request.ServiceName);
        AddFilter(filters, "level", request.Level);
        AddFilter(filters, "integrationType", request.IntegrationType);

        var query =
            "{job=\"integration-logs\"} | json | " +
            string.Join(" | ", filters);

        var limit = Math.Clamp(request.Limit, 1, DefaultLimit);

        return QueryLogsAsync(
            query,
            request.StartTime,
            request.EndTime,
            limit,
            cancellationToken);
    }

    private static void AddFilter(
        List<string> filters,
        string field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(
                $"{field}=\"{EscapeLogQlString(value.Trim())}\"");
        }
    }


    private static string EscapeLogQlString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }



    private static LokiLogEntry MapToLogEntry(
        string timestampValue,
        string rawLogLine,
        Dictionary<string, string> labels)
    {
        var entry = new LokiLogEntry
        {
            Timestamp = ParseLokiTimestamp(timestampValue),
            RawLogLine = rawLogLine,
            Message = rawLogLine,

            // Keep only metadata that is not already represented
            // by our normalized properties.
            SourceMetadata = labels
                .Where(x => !StandardPropertyNames.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value)
        };

        // Fallback: extract values from Loki metadata.
        entry.Level = GetLabel(labels, "level")
            ?? GetLabel(labels, "detected_level")
            ?? "Unknown";

        entry.ServiceName = GetLabel(labels, "serviceName")
            ?? GetLabel(labels, "service_name");

        entry.Environment = GetLabel(labels, "environment");

        entry.IntegrationType = GetLabel(labels, "integrationType");

        entry.EventType = GetLabel(labels, "eventType");

        entry.TransactionId = GetLabel(labels, "transactionId")
            ?? GetLabel(labels, "transaction_id");

        entry.TraceId = GetLabel(labels, "traceId")
            ?? GetLabel(labels, "trace_id");

        entry.Status = GetLabel(labels, "status");

        entry.ErrorCode = GetLabel(labels, "errorCode");

        if (double.TryParse(
            GetLabel(labels, "durationMs"),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var duration))
        {
            entry.DurationMs = duration;
        }

        // Parse the original JSON log line.
        try
        {
            using var document = JsonDocument.Parse(rawLogLine);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return entry;

            var root = document.RootElement;

            // Prefer actual log properties over metadata fallbacks.
            entry.Level = GetString(root, "level") ?? entry.Level;

            entry.ServiceName =
                GetString(root, "serviceName") ?? entry.ServiceName;

            entry.Environment =
                GetString(root, "environment") ?? entry.Environment;

            entry.IntegrationType =
                GetString(root, "integrationType") ?? entry.IntegrationType;

            entry.EventType =
                GetString(root, "eventType") ?? entry.EventType;

            entry.TransactionId =
                GetString(root, "transactionId") ?? entry.TransactionId;

            entry.TraceId =
                GetString(root, "traceId") ?? entry.TraceId;

            entry.Status =
                GetString(root, "status") ?? entry.Status;

            entry.ErrorCode =
                GetString(root, "errorCode") ?? entry.ErrorCode;

            entry.Message =
                GetString(root, "message") ?? rawLogLine;

            if (TryGetPropertyIgnoreCase(
                    root, "durationMs", out var durationElement) &&
                durationElement.ValueKind == JsonValueKind.Number &&
                durationElement.TryGetDouble(out var durationMs))
            {
                entry.DurationMs = durationMs;
            }

            // Preserve integration-specific properties.
            foreach (var property in root.EnumerateObject())
            {
                if (!StandardPropertyNames.Contains(property.Name))
                {
                    entry.AdditionalProperties[property.Name] =
                        property.Value.Clone();
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON logs are still valid log entries.
            // Preserve the original line as the message.
        }

        return entry;
    }


    private static string? GetLabel(
        Dictionary<string, string> labels,
        string name)
    {
        var match = labels.FirstOrDefault(
            x => string.Equals(
                x.Key,
                name,
                StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(match.Value)
            ? null
            : match.Value;
    }

    private static string? GetString(
        JsonElement element,
        string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(
                property.Name,
                propertyName,
                StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static DateTimeOffset ParseLokiTimestamp(string timestamp)
    {
        if (!long.TryParse(
            timestamp,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var nanoseconds))
        {
            throw new FormatException(
                $"Invalid Loki timestamp: {timestamp}");
        }

        return DateTimeOffset.UnixEpoch.AddTicks(nanoseconds / 100);
    }


    private static readonly HashSet<string> StandardPropertyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
        "timestamp",
        "level",
        "detected_level",
        "level_extracted",

        "serviceName",
        "service_name",

        "environment",
        "environment_extracted",

        "integrationType",
        "integration_type",

        "eventType",
        "event_type",

        "transactionId",
        "transaction_id",

        "traceId",
        "trace_id",

        "status",

        "errorCode",
        "error_code",

        "message",

        "durationMs",
        "duration_ms"
        };

}
