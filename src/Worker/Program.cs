using SampleApp.ServiceDefaults;
using Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// ElasticMQ（SQS 互換キュー）クライアント
builder.Services.AddElasticMqSqs(builder.Configuration);

builder.Services.AddHostedService<QueueWorker>();

var host = builder.Build();
host.Run();
