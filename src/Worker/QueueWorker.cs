using Amazon.SQS;
using Amazon.SQS.Model;
using SampleApp.ServiceDefaults;

namespace Worker;

/// <summary>
/// ElasticMQ のキューを定期的にポーリングし、メッセージを取得・処理・削除する。
/// </summary>
public class QueueWorker(
    ILogger<QueueWorker> logger,
    IAmazonSQS sqs,
    SqsOptions options) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // キューが用意できるまで待ってから URL を取得
        var queueUrl = await sqs.EnsureQueueAsync(options.QueueName, stoppingToken);
        logger.LogInformation("Polling queue {QueueName} ({QueueUrl})", options.QueueName, queueUrl);

        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await ReceiveAndDeleteAsync(queueUrl, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while polling queue");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ReceiveAndDeleteAsync(string queueUrl, CancellationToken ct)
    {
        var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 2, // ロングポーリング
        }, ct);

        if (response.Messages is not { Count: > 0 })
        {
            return;
        }

        foreach (var message in response.Messages)
        {
            logger.LogInformation("Received message {MessageId}: {Body}", message.MessageId, message.Body);

            // ここで本来の処理を行う（今回はログ出力のみ）

            await sqs.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = message.ReceiptHandle,
            }, ct);

            logger.LogInformation("Deleted message {MessageId}", message.MessageId);
        }
    }
}
