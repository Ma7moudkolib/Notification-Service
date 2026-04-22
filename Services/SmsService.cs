using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotificationService.Configuration;
using NotificationService.Models;
using Polly;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace NotificationService.Services;

public sealed class SmsService(
    ILogger<SmsService> logger,
    IOptions<TwilioSettings> twilioOptions,
    [FromKeyedServices("sms")] IAsyncPolicy retryPolicy)
    : ISmsService
{
    private readonly TwilioSettings _twilioSettings = twilioOptions.Value;

    public async Task SendAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        await retryPolicy.ExecuteAsync(async ct =>
        {
            ct.ThrowIfCancellationRequested();
            TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

            await MessageResource.CreateAsync(
                to: new PhoneNumber(request.Recipient),
                from: new PhoneNumber(_twilioSettings.FromNumber),
                body: request.Message).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "SMS sent. MessageId: {MessageId}, Recipient: {Recipient}",
            request.MessageId,
            request.Recipient);
    }
}
