using NotificationService.Models;

namespace NotificationService.Services;

public interface INotificationProcessor
{
    Task ProcessAsync(NotificationRequest request, CancellationToken cancellationToken);
}
