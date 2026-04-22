using NotificationService.Configuration;
using NotificationService.Consumers;
using NotificationService.Services;
using Polly;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<KafkaOptions>()
    .Bind(builder.Configuration.GetSection(KafkaOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<RedisOptions>()
    .Bind(builder.Configuration.GetSection(RedisOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(EmailSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<TwilioSettings>()
    .Bind(builder.Configuration.GetSection(TwilioSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
{
    var redisOptions = serviceProvider.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<RedisOptions>>().Value;

    return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
});

builder.Services.AddKeyedSingleton<IAsyncPolicy>(
    "email",
    (_, _) => BuildRetryPolicy("email"));

builder.Services.AddKeyedSingleton<IAsyncPolicy>(
    "sms",
    (_, _) => BuildRetryPolicy("sms"));

builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<ISmsService, SmsService>();
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();
builder.Services.AddSingleton<INotificationProcessor, NotificationProcessor>();
builder.Services.AddHostedService<KafkaConsumerWorker>();

await builder.Build().RunAsync();

static IAsyncPolicy BuildRetryPolicy(string channel) =>
    Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (exception, delay, retryCount, _) =>
            {
                Console.WriteLine(
                    "[Retry] Channel: {0}, Attempt: {1}, Delay: {2}s, Error: {3}",
                    channel,
                    retryCount,
                    delay.TotalSeconds,
                    exception.Message);
            });
