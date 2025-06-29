namespace ShoppingCart.Common.Patterns.Observer;

public interface INotificationObserver
{
    string ObserverType { get; }
    Task UpdateAsync(NotificationEvent notificationEvent);
}

public class NotificationEvent
{
    public string EventType { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string NotificationType { get; set; } = "Info";
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public interface INotificationSubject
{
    void Attach(INotificationObserver observer);
    void Detach(INotificationObserver observer);
    Task NotifyAsync(NotificationEvent notificationEvent);
} 