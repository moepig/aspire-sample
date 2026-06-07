using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Web.Tests;

/// <summary>Web 側のキュー書き込み（送信）パスを検証する。</summary>
[Collection(nameof(AppHostCollection))]
public class ElasticMqPublishTests(AppHostFixture fixture)
{
    [Fact]
    public async Task Can_publish_message()
    {
        var ct = TestContext.Current.CancellationToken;
        await fixture.WaitForRunningAsync("elasticmq", ct);

        var config = new AmazonSQSConfig
        {
            ServiceURL = fixture.GetSqsServiceUrl(),
            AuthenticationRegion = "elasticmq",
        };
        using var sqs = new AmazonSQSClient(new BasicAWSCredentials("local", "local"), config);

        var queueUrl = (await sqs.CreateQueueAsync("publish-test-queue", ct)).QueueUrl;

        var send = await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = "hello-from-web",
        }, ct);

        Assert.False(string.IsNullOrEmpty(send.MessageId));

        var received = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5,
        }, ct);

        var message = Assert.Single(received.Messages);
        Assert.Equal("hello-from-web", message.Body);
    }
}
