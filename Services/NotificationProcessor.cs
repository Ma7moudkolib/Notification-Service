using NotificationService.Models;

namespace NotificationService.Services;

public sealed class NotificationProcessor(
    IEmailService emailService,
    ISmsService smsService,
    IRedisCacheService redisCacheService,
    ILogger<NotificationProcessor> logger)
    : INotificationProcessor
{
    public async Task ProcessAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            throw new InvalidOperationException("Notification message must contain a MessageId.");
        }

        var isFirstProcessingAttempt = await redisCacheService
            .TryMarkAsProcessedAsync(request.MessageId, cancellationToken)
            .ConfigureAwait(false);

        if (!isFirstProcessingAttempt)
        {
            logger.LogInformation(
                "Skipping duplicate notification. MessageId: {MessageId}",
                request.MessageId);
            return;
        }

        try
        {
            switch (request.Type.Trim().ToLowerInvariant())
            {
                case "email":
                    await emailService.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    break;

                case "sms":
                    await smsService.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported notification type '{request.Type}'.");
            }

            logger.LogInformation(
                "Notification processed successfully. MessageId: {MessageId}, Type: {Type}",
                request.MessageId,
                request.Type);
        }
        catch
        {
            await redisCacheService
                .RemoveProcessedMarkerAsync(request.MessageId, cancellationToken)
                .ConfigureAwait(false);

            throw;
        }
    }
}
