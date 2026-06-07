var builder = DistributedApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// データストア / キュー（すべてコンテナで起動）
// ---------------------------------------------------------------------------

// MySQL : Todo の永続化先
var mysql = builder.AddMySql("mysql");

// 永続ボリュームは通常実行(dotnet run)のみで使う。
// 統合テストはソリューション単位で複数の AppHost を並列起動するため、同名の
// 名前付きボリュームを共有すると InnoDB の ibdata1 ロック競合(error 11)で
// 2 つ目の MySQL が起動できずハングする。テスト時は揮発ストレージにする。
if (builder.Configuration["SampleApp:DisableDataVolume"] != "true")
{
    mysql.WithDataVolume(); // コンテナ再作成をまたいでデータを保持
}

var todoDb = mysql.AddDatabase("todos");

// Valkey : Todo 一覧のキャッシュ（Redis 互換）
var valkey = builder.AddValkey("valkey");

// ElasticMQ : SQS 互換のメッセージキュー。専用の統合は無いので汎用コンテナとして起動する。
//   9324 = SQS API エンドポイント / 9325 = 管理 UI
const string queueName = "todo-events";
var elasticmq = builder.AddContainer("elasticmq", "softwaremill/elasticmq-native", "1.6.11")
    .WithHttpEndpoint(targetPort: 9324, name: "sqs")
    .WithHttpEndpoint(targetPort: 9325, name: "ui");

var sqsEndpoint = elasticmq.GetEndpoint("sqs");

// ---------------------------------------------------------------------------
// アプリケーション
// ---------------------------------------------------------------------------

// Web : Minimal API。MySQL / Valkey / ElasticMQ すべてに読み書きする。
builder.AddProject<Projects.Web>("web")
    .WithReference(todoDb).WaitFor(todoDb)
    .WithReference(valkey).WaitFor(valkey)
    .WithEnvironment("Sqs__ServiceUrl", sqsEndpoint)
    .WithEnvironment("Sqs__QueueName", queueName)
    .WaitFor(elasticmq)
    .WithExternalHttpEndpoints();

// Worker : ElasticMQ のキューを定期的にポーリングしてメッセージを取得・削除する。
builder.AddProject<Projects.Worker>("worker")
    .WithReference(todoDb).WaitFor(todoDb)
    .WithEnvironment("Sqs__ServiceUrl", sqsEndpoint)
    .WithEnvironment("Sqs__QueueName", queueName)
    .WaitFor(elasticmq);

builder.Build().Run();
