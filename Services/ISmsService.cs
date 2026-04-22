using NotificationService.Models;

namespace NotificationService.Services;

public interface ISmsService
{
    Task SendAsync(NotificationRequest request, CancellationToken cancellationToken);
}
