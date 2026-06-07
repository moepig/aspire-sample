using Aspire.Hosting;
using MySqlConnector;

namespace Web.Tests;

[Collection(nameof(AppHostCollection))]
public class MySqlTests(AppHostFixture fixture)
{
    [Fact]
    public async Task Can_write_and_read_row()
    {
        var ct = TestContext.Current.CancellationToken;
        await fixture.WaitForHealthyAsync("mysql", ct);
        var connectionString = await fixture.App.GetConnectionStringAsync("todos", ct);
        Assert.NotNull(connectionString);

        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using (var create = conn.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS test_items (
                    id INT PRIMARY KEY AUTO_INCREMENT,
                    name VARCHAR(100) NOT NULL
                );
                """;
            await create.ExecuteNonQueryAsync(ct);
        }

        await using (var insert = conn.CreateCommand())
        {
            insert.CommandText = "INSERT INTO test_items (name) VALUES (@name);";
            insert.Parameters.AddWithValue("@name", "hello-mysql");
            await insert.ExecuteNonQueryAsync(ct);
        }

        await using var select = conn.CreateCommand();
        select.CommandText = "SELECT name FROM test_items ORDER BY id DESC LIMIT 1;";
        var value = (string?)await select.ExecuteScalarAsync(ct);

        Assert.Equal("hello-mysql", value);
    }
}
