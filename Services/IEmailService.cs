using NotificationService.Models;

namespace NotificationService.Services;

public interface IEmailService
{
    Task SendAsync(NotificationRequest request, CancellationToken cancellationToken);
}
