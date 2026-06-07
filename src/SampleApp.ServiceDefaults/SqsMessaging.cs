using Amazon.Runtime;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SampleApp.ServiceDefaults;

/// <summary>
/// ElasticMQ（SQS 互換キュー）の接続設定。AppHost から環境変数で渡される。
///   Sqs__ServiceUrl  ... ElasticMQ の SQS エンドポイント URL
///   Sqs__QueueName   ... 使用するキュー名
/// </summary>
public sealed class SqsOptions
{
    public string ServiceUrl { get; set; } = "http://localhost:9324";
    public string QueueName { get; set; } = "todo-events";
}

public static class SqsMessaging
{
    /// <summary>
    /// ElasticMQ を指す <see cref="IAmazonSQS"/> と <see cref="SqsOptions"/> を DI に登録する。
    /// ElasticMQ は認証を検証しないのでダミーの資格情報を渡す。
    /// </summary>
    public static IServiceCollection AddElasticMqSqs(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new SqsOptions();
        configuration.GetSection("Sqs").Bind(options);
        services.AddSingleton(options);

        services.AddSingleton<IAmazonSQS>(_ =>
        {
            var config = new AmazonSQSConfig
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = "elasticmq",
            };
            return new AmazonSQSClient(new BasicAWSCredentials("local", "local"), config);
        });

        return services;
    }

    /// <summary>
    /// キューが無ければ作成して URL を返す（ElasticMQ では冪等）。
    /// </summary>
    public static async Task<string> EnsureQueueAsync(this IAmazonSQS sqs, string queueName, CancellationToken ct = default)
    {
        var response = await sqs.CreateQueueAsync(queueName, ct);
        return response.QueueUrl;
    }
}
