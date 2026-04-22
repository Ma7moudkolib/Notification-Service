namespace NotificationService.Models;

public sealed record NotificationRequest(
    string MessageId,
    string Type,
    string Recipient,
    string? Subject,
    string Message);
