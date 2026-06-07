using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Worker.Tests;

/// <summary>
/// Worker 側のキュー読み取り（受信・削除）パスを検証する。
/// シードメッセージを投入し、受信 → 削除 → 空になることを確認する。
/// </summary>
[Collection(nameof(AppHostCollection))]
public class QueueConsumeTests(AppHostFixture fixture)
{
    [Fact]
    public async Task Can_receive_and_delete_message()
    {
        var ct = TestContext.Current.CancellationToken;
        await fixture.WaitForRunningAsync("elasticmq", ct);

        var config = new AmazonSQSConfig
        {
            ServiceURL = fixture.GetSqsServiceUrl(),
            AuthenticationRegion = "elasticmq",
        };
        using var sqs = new AmazonSQSClient(new BasicAWSCredentials("local", "local"), config);

        var queueUrl = (await sqs.CreateQueueAsync("consume-test-queue", ct)).QueueUrl;

        // シード: Worker が処理する想定のメッセージを投入
        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = "to-be-consumed",
        }, ct);

        // 受信
        var received = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5,
        }, ct);

        var message = Assert.Single(received.Messages);
        Assert.Equal("to-be-consumed", message.Body);

        // 削除
        await sqs.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = message.ReceiptHandle,
        }, ct);

        // 削除後はキューが空であること
        var after = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 1,
        }, ct);
        Assert.True(after.Messages is null || after.Messages.Count == 0);
    }
}
