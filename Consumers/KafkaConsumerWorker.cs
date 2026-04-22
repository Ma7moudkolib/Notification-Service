using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NotificationService.Configuration;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.Consumers;

public sealed class KafkaConsumerWorker(
    ILogger<KafkaConsumerWorker> logger,
    IOptions<KafkaOptions> kafkaOptions,
    INotificationProcessor notificationProcessor)
    : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly KafkaOptions _kafkaOptions = kafkaOptions.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
    }

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = _kafkaOptions.GroupId,
            AutoOffsetReset = MapOffsetReset(_kafkaOptions.AutoOffsetReset),
            EnableAutoCommit = false,
            AllowAutoCreateTopics = false
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, error) =>
            {
                if (error.IsFatal)
                {
                    logger.LogCritical("Kafka fatal error: {Reason}", error.Reason);
                    return;
                }

                logger.LogWarning("Kafka error: {Reason}", error.Reason);
            })
            .Build();

        consumer.Subscribe(_kafkaOptions.Topic);
        logger.LogInformation(
            "Kafka consumer started. Topic: {Topic}, GroupId: {GroupId}",
            _kafkaOptions.Topic,
            _kafkaOptions.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);

                    if (string.IsNullOrWhiteSpace(consumeResult.Message.Value))
                    {
                        logger.LogWarning(
                            "Empty Kafka message skipped at topic-partition-offset {TopicPartitionOffset}",
                            consumeResult.TopicPartitionOffset);
                        consumer.Commit(consumeResult);
                        continue;
                    }

                    var notification = JsonSerializer.Deserialize<NotificationRequest>(
                        consumeResult.Message.Value,
                        SerializerOptions);

                    if (notification is null)
                    {
                        logger.LogWarning(
                            "Invalid Kafka payload skipped at topic-partition-offset {TopicPartitionOffset}",
                            consumeResult.TopicPartitionOffset);
                        consumer.Commit(consumeResult);
                        continue;
                    }

                    notificationProcessor
                        .ProcessAsync(notification, stoppingToken)
                        .GetAwaiter()
                        .GetResult();

                    consumer.Commit(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Kafka consume error");
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Failed to deserialize Kafka message");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error while processing a notification");
                }
            }
        }
        finally
        {
            consumer.Close();
            logger.LogInformation("Kafka consumer stopped");
        }
    }

    private static AutoOffsetReset MapOffsetReset(AutoOffsetResetMode offsetResetMode) =>
        offsetResetMode == AutoOffsetResetMode.Latest
            ? AutoOffsetReset.Latest
            : AutoOffsetReset.Earliest;
}
