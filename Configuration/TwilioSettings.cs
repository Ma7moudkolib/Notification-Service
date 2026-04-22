using System.ComponentModel.DataAnnotations;

namespace NotificationService.Configuration;

public sealed class TwilioSettings
{
    public const string SectionName = "Twilio";

    [Required]
    public string AccountSid { get; init; } = string.Empty;

    [Required]
    public string AuthToken { get; init; } = string.Empty;

    [Required]
    public string FromNumber { get; init; } = string.Empty;
}
