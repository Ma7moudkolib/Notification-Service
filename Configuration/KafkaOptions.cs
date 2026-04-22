using System.ComponentModel.DataAnnotations;

namespace NotificationService.Configuration;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    [Required]
    public string BootstrapServers { get; init; } = "kafka:9092";

    [Required]
    public string Topic { get; init; } = "notifications-topic";

    [Required]
    public string GroupId { get; init; } = "notification-service";

    public AutoOffsetResetMode AutoOffsetReset { get; init; } = AutoOffsetResetMode.Earliest;
}

public enum AutoOffsetResetMode
{
    Earliest,
    Latest
}
