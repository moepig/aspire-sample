using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Worker.Tests;

/// <summary>
/// AppHost（=すべてのコンテナ）を1度だけ起動し、テスト全体で共有するフィクスチャ。
/// 実行には Docker が必要。
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public DistributedApplication App => _app ?? throw new InvalidOperationException("App not started");

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.SampleApp_AppHost>(ct);
        _app = await builder.BuildAsync(ct);
        await _app.StartAsync(ct);
    }

    /// <summary>リソースが Running になるまで待つ（最大2分）。</summary>
    public async Task WaitForRunningAsync(string resourceName, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(2));
        await App.ResourceNotifications.WaitForResourceAsync(
            resourceName, KnownResourceStates.Running, cts.Token);
    }

    public string GetSqsServiceUrl() =>
        App.GetEndpoint("elasticmq", "sqs").ToString();

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

[CollectionDefinition(nameof(AppHostCollection))]
public sealed class AppHostCollection : ICollectionFixture<AppHostFixture>;
