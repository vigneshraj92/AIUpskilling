using Microsoft.Extensions.Logging;

namespace ShoppingCart.Common.Patterns.Observer;

public class SMSNotificationObserver : INotificationObserver
{
    private readonly ILogger<SMSNotificationObserver> _logger;

    public SMSNotificationObserver(ILogger<SMSNotificationObserver> logger)
    {
        _logger = logger;
    }

    public string ObserverType => "SMS";

    public async Task UpdateAsync(NotificationEvent notificationEvent)
    {
        _logger.LogInformation("Sending SMS notification to user {UserId}: {Title}", 
            notificationEvent.UserId, notificationEvent.Title);

        // Simulate SMS sending
        await Task.Delay(300);

        _logger.LogInformation("SMS notification sent successfully to user {UserId}", 
            notificationEvent.UserId);

        // In a real implementation, this would integrate with an SMS service
        // like Twilio, AWS SNS, or similar
    }
} 