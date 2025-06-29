using Microsoft.Extensions.Logging;

namespace ShoppingCart.Common.Patterns.Observer;

public class NotificationSubject : INotificationSubject
{
    private readonly List<INotificationObserver> _observers = new();
    private readonly ILogger<NotificationSubject> _logger;

    public NotificationSubject(ILogger<NotificationSubject> logger)
    {
        _logger = logger;
    }

    public void Attach(INotificationObserver observer)
    {
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
            _logger.LogInformation("Observer {ObserverType} attached", observer.ObserverType);
        }
    }

    public void Detach(INotificationObserver observer)
    {
        if (_observers.Contains(observer))
        {
            _observers.Remove(observer);
            _logger.LogInformation("Observer {ObserverType} detached", observer.ObserverType);
        }
    }

    public async Task NotifyAsync(NotificationEvent notificationEvent)
    {
        _logger.LogInformation("Notifying {ObserverCount} observers about event: {EventType}", 
            _observers.Count, notificationEvent.EventType);

        var tasks = _observers.Select(observer => 
        {
            try
            {
                return observer.UpdateAsync(notificationEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying observer {ObserverType}", observer.ObserverType);
                return Task.CompletedTask;
            }
        });

        await Task.WhenAll(tasks);
    }
} 