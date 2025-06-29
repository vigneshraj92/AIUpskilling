using Microsoft.Extensions.Logging;

namespace ShoppingCart.Common.Patterns.Observer;

public class PushNotificationObserver : INotificationObserver
{
    private readonly ILogger<PushNotificationObserver> _logger;

    public PushNotificationObserver(ILogger<PushNotificationObserver> logger)
    {
        _logger = logger;
    }

    public string ObserverType => "Push";

    public async Task UpdateAsync(NotificationEvent notificationEvent)
    {
        _logger.LogInformation("Sending push notification to user {UserId}: {Title}", 
            notificationEvent.UserId, notificationEvent.Title);

        // Simulate push notification sending
        await Task.Delay(200);

        _logger.LogInformation("Push notification sent successfully to user {UserId}", 
            notificationEvent.UserId);

        // In a real implementation, this would integrate with push notification services
        // like Firebase Cloud Messaging, Apple Push Notification Service, etc.
    }
} 