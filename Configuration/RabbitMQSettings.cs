using System.ComponentModel.DataAnnotations;

namespace NotificationService.Configuration;

public sealed class RabbitMQSettings
{
    public const string SectionName = "RabbitMQ";

    [Required]
    public string HostName { get; init; } = "rabbitmq";

    [Required]
    public string UserName { get; init; } = "guest";

    [Required]
    public string Password { get; init; } = "guest";

    [Range(1, 65535)]
    public int Port { get; init; } = 5672;
}
