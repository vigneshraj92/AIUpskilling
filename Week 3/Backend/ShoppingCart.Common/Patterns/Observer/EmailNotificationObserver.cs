using Microsoft.Extensions.Logging;

namespace ShoppingCart.Common.Patterns.Observer;

public class EmailNotificationObserver : INotificationObserver
{
    private readonly ILogger<EmailNotificationObserver> _logger;

    public EmailNotificationObserver(ILogger<EmailNotificationObserver> logger)
    {
        _logger = logger;
    }

    public string ObserverType => "Email";

    public async Task UpdateAsync(NotificationEvent notificationEvent)
    {
        _logger.LogInformation("Sending email notification to user {UserId}: {Title}", 
            notificationEvent.UserId, notificationEvent.Title);

        // Simulate email sending
        await Task.Delay(500);

        _logger.LogInformation("Email notification sent successfully to user {UserId}", 
            notificationEvent.UserId);

        // In a real implementation, this would integrate with an email service
        // like SendGrid, MailKit, or SMTP
    }
} 