
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IntelligentMonitoring.Notification.Application.Abstractions;
using IntelligentMonitoring.Notification.Application.Models;
using IntelligentMonitoring.Notification.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace IntelligentMonitoring.Notification.Infrastructure.Providers.Slack;

public sealed class SlackNotificationProvider(
    HttpClient httpClient,
    IOptions<SlackOptions> options)
    : INotificationProvider
{
    private readonly SlackOptions _options = options.Value;

    public NotificationChannel Channel => NotificationChannel.Slack;

    public async Task<NotificationDispatchResult> SendAsync(
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Event);

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            throw new InvalidOperationException(
                "Slack Bot Token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.Destination))
        {
            throw new ArgumentException(
                "Slack channel destination is required.");
        }

        var payload = new SlackMessageRequest
        {
            Channel = request.Destination,
            Text = BuildMessage(request.Event)
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "chat.postMessage");

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _options.BotToken);

        httpRequest.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(
            httpRequest,
            cancellationToken);

        var slackResponse = await response.Content
            .ReadFromJsonAsync<SlackMessageResponse>(
                cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return CreateFailureResult(
                $"Slack HTTP request failed. Status: {(int)response.StatusCode}.");
        }

        if (slackResponse is null)
        {
            return CreateFailureResult(
                "Slack returned an empty or invalid response.");
        }

        if (!slackResponse.Ok)
        {
            return CreateFailureResult(
                $"Slack API error: {slackResponse.Error ?? "Unknown error"}");
        }

        return new NotificationDispatchResult
        {
            IsSuccessful = true,
            ExternalMessageId = slackResponse.Timestamp,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static string BuildMessage(
        IntelligentMonitoring.Notification.Contracts.Events.InvestigationCompletedEvent failure)
    {
        return $"""
        :rotating_light: *Integration Failure Investigation Completed*

        *Environment:* {EscapeSlack(failure.Environment)}
        *Service:* {EscapeSlack(failure.ServiceName)}
        *Integration Type:* {EscapeSlack(failure.IntegrationType)}

        *Transaction ID:* `{EscapeSlack(failure.TransactionId)}`
        *Trace ID:* `{EscapeSlack(failure.TraceId)}`

        *Error Code:* {EscapeSlack(failure.ErrorCode ?? "N/A")}
        *Error Message:* {EscapeSlack(failure.ErrorMessage ?? "N/A")}

        *AI Summary:*
        {EscapeSlack(failure.AiSummary ?? "Not available")}

        *Probable Root Cause:*
        {EscapeSlack(failure.AiProbableRootCause ?? "Not available")}

        *Business Impact:*
        {EscapeSlack(failure.AiBusinessImpact ?? "Not available")}

        *Investigation Status:* {EscapeSlack(failure.InvestigationStatus)}
        *Occurred At (UTC):* {failure.OccurredAtUtc:yyyy-MM-dd HH:mm:ss}
        *Completed At (UTC):* {failure.CompletedAtUtc:yyyy-MM-dd HH:mm:ss}
        """;
    }

    private static string EscapeSlack(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static NotificationDispatchResult CreateFailureResult(
        string errorMessage)
    {
        return new NotificationDispatchResult
        {
            IsSuccessful = false,
            ErrorMessage = errorMessage,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private sealed record SlackMessageRequest
    {
        public required string Channel { get; init; }

        public required string Text { get; init; }
    }

    private sealed record SlackMessageResponse
    {
        public bool Ok { get; init; }

        public string? Error { get; init; }

        public string? Timestamp { get; init; }
    }
}
