using System.ComponentModel.DataAnnotations;

namespace NotificationService.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required]
    public string ConnectionString { get; init; } = "redis:6379";

    [Required]
    public string KeyPrefix { get; init; } = "notifications:processed:";

    public TimeSpan Expiry { get; init; } = TimeSpan.FromHours(24);
}
