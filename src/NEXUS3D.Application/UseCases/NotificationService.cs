using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;

namespace NEXUS3D.Application.UseCases;

/// <summary>
/// Thread-safe in-memory notification hub for publishing status events and alerts to the UI.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly List<NotificationMessage> _notifications = [];
    private readonly Lock _syncLock = new();
    private const int MaxStoredNotifications = 100;

    public event EventHandler<NotificationMessage>? NotificationPublished;

    public IReadOnlyList<NotificationMessage> GetNotifications()
    {
        lock (_syncLock)
        {
            return _notifications.ToList();
        }
    }

    public void Publish(
        string title,
        string message,
        NotificationSeverity severity = NotificationSeverity.Info,
        string sourceModule = "System")
    {
        var item = new NotificationMessage(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(title) ? "Notification" : title,
            message ?? string.Empty,
            severity,
            string.IsNullOrWhiteSpace(sourceModule) ? "System" : sourceModule,
            DateTimeOffset.UtcNow,
            IsRead: false);

        lock (_syncLock)
        {
            _notifications.Insert(0, item);
            if (_notifications.Count > MaxStoredNotifications)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }
        }

        NotificationPublished?.Invoke(this, item);
    }

    public void MarkAllAsRead()
    {
        lock (_syncLock)
        {
            for (int i = 0; i < _notifications.Count; i++)
            {
                if (!_notifications[i].IsRead)
                {
                    _notifications[i] = _notifications[i] with { IsRead = true };
                }
            }
        }
    }

    public void ClearAll()
    {
        lock (_syncLock)
        {
            _notifications.Clear();
        }
    }
}

