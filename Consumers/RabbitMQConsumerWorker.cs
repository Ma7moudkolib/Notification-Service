using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NotificationService.Configuration;
using NotificationService.Models;
using NotificationService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Consumers;

public sealed class RabbitMQConsumerWorker(
    ILogger<RabbitMQConsumerWorker> logger,
    IOptions<RabbitMQSettings> rabbitMqOptions,
    INotificationProcessor notificationProcessor)
    : BackgroundService
{
    private const string ExchangeName = "notifications-exchange";
    private const string QueueName = "notifications-queue";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMQSettings _rabbitMqSettings = rabbitMqOptions.Value;

    private IConnection? _connection;
    private IModel? _channel;
    private string? _consumerTag;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqSettings.HostName,
            UserName = _rabbitMqSettings.UserName,
            Password = _rabbitMqSettings.Password,
            Port = _rabbitMqSettings.Port,
            DispatchConsumersAsync = false
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null);

        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.QueueBind(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: string.Empty);

        _channel.BasicQos(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (_, eventArgs) =>
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            ProcessMessage(eventArgs, stoppingToken);
        };

        _consumerTag = _channel.BasicConsume(
            queue: QueueName,
            autoAck: false,
            consumer: consumer);

        logger.LogInformation(
            "RabbitMQ consumer started. Exchange: {Exchange}, Queue: {Queue}, Host: {Host}:{Port}",
            ExchangeName,
            QueueName,
            _rabbitMqSettings.HostName,
            _rabbitMqSettings.Port);

        stoppingToken.Register(() => logger.LogInformation("RabbitMQ consumer stopping..."));

        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void ProcessMessage(BasicDeliverEventArgs eventArgs, CancellationToken stoppingToken)
    {
        if (_channel is null)
        {
            logger.LogError("RabbitMQ channel unavailable; message cannot be processed.");
            return;
        }

        try
        {
            var body = eventArgs.Body.ToArray();
            var payload = Encoding.UTF8.GetString(body);

            if (string.IsNullOrWhiteSpace(payload))
            {
                logger.LogWarning(
                    "Empty RabbitMQ message skipped at delivery tag {DeliveryTag}",
                    eventArgs.DeliveryTag);

                _channel.BasicAck(deliveryTag: eventArgs.DeliveryTag, multiple: false);
                return;
            }

            var notification = JsonSerializer.Deserialize<NotificationRequest>(payload, SerializerOptions);

            if (notification is null)
            {
                logger.LogWarning(
                    "Invalid RabbitMQ payload skipped at delivery tag {DeliveryTag}",
                    eventArgs.DeliveryTag);

                _channel.BasicAck(deliveryTag: eventArgs.DeliveryTag, multiple: false);
                return;
            }

            notificationProcessor
                .ProcessAsync(notification, stoppingToken)
                .GetAwaiter()
                .GetResult();

            _channel.BasicAck(deliveryTag: eventArgs.DeliveryTag, multiple: false);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize RabbitMQ message");
            _channel.BasicNack(deliveryTag: eventArgs.DeliveryTag, multiple: false, requeue: false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "RabbitMQ message handling canceled at delivery tag {DeliveryTag}",
                eventArgs.DeliveryTag);

            _channel.BasicNack(deliveryTag: eventArgs.DeliveryTag, multiple: false, requeue: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while processing RabbitMQ notification");
            _channel.BasicNack(deliveryTag: eventArgs.DeliveryTag, multiple: false, requeue: true);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_channel is not null && !string.IsNullOrWhiteSpace(_consumerTag))
            {
                _channel.BasicCancel(_consumerTag);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while cancelling RabbitMQ consumer");
        }

        _channel?.Dispose();
        _connection?.Dispose();

        logger.LogInformation("RabbitMQ consumer stopped");

        return base.StopAsync(cancellationToken);
    }
}
