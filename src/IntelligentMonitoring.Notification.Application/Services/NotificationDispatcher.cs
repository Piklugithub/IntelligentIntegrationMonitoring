
using IntelligentMonitoring.Notification.Application.Abstractions;
using IntelligentMonitoring.Notification.Application.Models;

namespace IntelligentMonitoring.Notification.Application.Services;

public sealed class NotificationDispatcher(
    IEnumerable<INotificationProvider> providers)
    : INotificationDispatcher
{
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationProvider>
        _providers = CreateProviderRegistry(providers);

    public async Task<NotificationDispatchResult> DispatchAsync(
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Event);

        if (string.IsNullOrWhiteSpace(request.Destination))
        {
            return CreateFailureResult(
                "Notification destination cannot be empty.");
        }

        if (!Enum.IsDefined(request.Channel))
        {
            return CreateFailureResult(
                $"Invalid notification channel: {request.Channel}.");
        }

        if (!_providers.TryGetValue(request.Channel, out var provider))
        {
            return CreateFailureResult(
                $"No provider is registered for channel '{request.Channel}'.");
        }

        try
        {
            return await provider.SendAsync(
                request,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CreateFailureResult(
                $"Notification delivery failed: {exception.Message}");
        }
    }

    private static IReadOnlyDictionary<NotificationChannel, INotificationProvider>
        CreateProviderRegistry(IEnumerable<INotificationProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var registry = new Dictionary<NotificationChannel, INotificationProvider>();

        foreach (var provider in providers)
        {
            if (provider is null)
            {
                throw new InvalidOperationException(
                    "A notification provider registration cannot be null.");
            }

            if (!registry.TryAdd(provider.Channel, provider))
            {
                throw new InvalidOperationException(
                    $"Multiple notification providers are registered " +
                    $"for channel '{provider.Channel}'.");
            }
        }

        return registry;
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
}
