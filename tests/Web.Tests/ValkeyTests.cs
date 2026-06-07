using Aspire.Hosting;
using StackExchange.Redis;

namespace Web.Tests;

[Collection(nameof(AppHostCollection))]
public class ValkeyTests(AppHostFixture fixture)
{
    [Fact]
    public async Task Can_set_and_get_value()
    {
        var ct = TestContext.Current.CancellationToken;
        await fixture.WaitForHealthyAsync("valkey", ct);
        var connectionString = await fixture.App.GetConnectionStringAsync("valkey", ct);
        Assert.NotNull(connectionString);

        await using var redis = await ConnectionMultiplexer.ConnectAsync(connectionString!);
        var db = redis.GetDatabase();

        await db.StringSetAsync("greeting", "hello-valkey");
        var value = await db.StringGetAsync("greeting");

        Assert.Equal("hello-valkey", value);
    }
}
