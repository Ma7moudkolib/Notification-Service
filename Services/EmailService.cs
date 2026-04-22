using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NotificationService.Configuration;
using NotificationService.Models;
using Polly;

namespace NotificationService.Services;

public sealed class EmailService(
    ILogger<EmailService> logger,
    IOptions<EmailSettings> emailOptions,
    [FromKeyedServices("email")] IAsyncPolicy retryPolicy)
    : IEmailService
{
    private readonly EmailSettings _emailSettings = emailOptions.Value;

    public async Task SendAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        await retryPolicy.ExecuteAsync(async ct =>
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_emailSettings.Username));
            message.To.Add(MailboxAddress.Parse(request.Recipient));
            message.Subject = request.Subject ?? string.Empty;
            message.Body = new TextPart("plain")
            {
                Text = request.Message
            };

            using var smtpClient = new SmtpClient();
            await smtpClient.ConnectAsync(
                _emailSettings.Host,
                _emailSettings.Port,
                SecureSocketOptions.StartTls,
                ct).ConfigureAwait(false);

            await smtpClient.AuthenticateAsync(
                _emailSettings.Username,
                _emailSettings.Password,
                ct).ConfigureAwait(false);

            await smtpClient.SendAsync(message, ct).ConfigureAwait(false);
            await smtpClient.DisconnectAsync(true, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Email sent. MessageId: {MessageId}, Recipient: {Recipient}, Subject: {Subject}",
            request.MessageId,
            request.Recipient,
            request.Subject);
    }
}
