using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using SampleApp.ServiceDefaults;
using StackExchange.Redis;
using Web;

var builder = WebApplication.CreateBuilder(args);

// Aspire の共通サービス（テレメトリ・ヘルスチェック・サービスディスカバリ）
builder.AddServiceDefaults();

// MySQL（Pomelo EF Core）: 接続文字列 "todos" は AppHost の WithReference(todoDb) で注入される
builder.AddMySqlDbContext<TodoDbContext>("todos");

// Valkey（Redis 互換）: 接続名 "valkey"
builder.AddRedisClient("valkey");

// ElasticMQ（SQS 互換キュー）
builder.Services.AddElasticMqSqs(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();

// 起動時にスキーマ作成 & キュー作成（参考サンプルなので EnsureCreated を使用）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
    await db.Database.EnsureCreatedAsync();

    var sqs = scope.ServiceProvider.GetRequiredService<IAmazonSQS>();
    var sqsOptions = scope.ServiceProvider.GetRequiredService<SqsOptions>();
    await sqs.EnsureQueueAsync(sqsOptions.QueueName);
}

const string CacheKey = "todos:all";

app.MapGet("/", () => "SampleApp Web - .NET Aspire reference");

// 一覧取得: まず Valkey キャッシュを見て、無ければ MySQL から読んでキャッシュする
app.MapGet("/api/todos", async (TodoDbContext db, IConnectionMultiplexer redis) =>
{
    var cache = redis.GetDatabase();
    var cached = await cache.StringGetAsync(CacheKey);
    if (cached.HasValue)
    {
        var fromCache = JsonSerializer.Deserialize<List<Todo>>((string)cached!);
        return Results.Ok(new { source = "valkey", items = fromCache });
    }

    var todos = await db.Todos.OrderByDescending(t => t.CreatedAt).ToListAsync();
    await cache.StringSetAsync(CacheKey, JsonSerializer.Serialize(todos), TimeSpan.FromSeconds(30));
    return Results.Ok(new { source = "mysql", items = todos });
});

// 作成: MySQL に保存 → キャッシュ無効化 → ElasticMQ にイベント送信
app.MapPost("/api/todos", async (
    CreateTodoRequest request,
    TodoDbContext db,
    IConnectionMultiplexer redis,
    IAmazonSQS sqs,
    SqsOptions sqsOptions) =>
{
    var todo = new Todo { Title = request.Title };
    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    // キャッシュを無効化
    await redis.GetDatabase().KeyDeleteAsync(CacheKey);

    // キューへイベント送信
    var queueUrl = await sqs.EnsureQueueAsync(sqsOptions.QueueName);
    var message = new TodoCreatedMessage(todo.Id, todo.Title, todo.CreatedAt);
    await sqs.SendMessageAsync(new SendMessageRequest
    {
        QueueUrl = queueUrl,
        MessageBody = JsonSerializer.Serialize(message),
    });

    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.Run();

public partial class Program;
