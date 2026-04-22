namespace NotificationService.Services;

public interface IRedisCacheService
{
    Task<bool> TryMarkAsProcessedAsync(string messageId, CancellationToken cancellationToken);
    Task RemoveProcessedMarkerAsync(string messageId, CancellationToken cancellationToken);
}
