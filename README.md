# SampleApp — .NET Aspire 参考アプリ

.NET 10 / .NET Aspire を使い、MySQL・Valkey・ElasticMQ をコンテナで起動して
読み書きする最小構成のサンプルです。

## 構成

| プロジェクト | 種類 | 役割 |
|---|---|---|
| `src/SampleApp.AppHost` | Aspire AppHost | 全コンテナ・アプリのオーケストレーション |
| `src/SampleApp.ServiceDefaults` | 共有ライブラリ | テレメトリ/ヘルスチェック/SQS クライアント設定 |
| `src/Web` | ASP.NET Core Minimal API | MySQL・Valkey・ElasticMQ すべてに読み書き |
| `src/Worker` | Worker Service | ElasticMQ のキューを定期ポーリングして取得・削除 |
| `tests/Web.Tests` | xUnit v3 | MySQL / Valkey / キュー送信の読み書きテスト |
| `tests/Worker.Tests` | xUnit v3 | キュー受信・削除のテスト |

## ミドルウェア（すべてコンテナ）

- **MySQL** — Todo の永続化（Pomelo EF Core）
- **Valkey** — Todo 一覧のキャッシュ（Redis 互換 / StackExchange.Redis）
- **ElasticMQ** — SQS 互換キュー（AWSSDK.SQS）。`9324`=API / `9325`=管理 UI

## データの流れ

```
POST /api/todos ──▶ MySQL 保存 ──▶ Valkey キャッシュ無効化 ──▶ ElasticMQ へイベント送信
GET  /api/todos ──▶ Valkey ヒット時はキャッシュ / ミス時は MySQL から読んでキャッシュ
Worker          ──▶ ElasticMQ を5秒間隔でポーリングし、受信したメッセージを削除
```

## 実行

前提: .NET 10 SDK、Docker

```bash
# Aspire ダッシュボード付きで全体を起動
dotnet run --project src/SampleApp.AppHost

# テスト（Docker でコンテナが起動します）
dotnet test SampleApp.slnx
```

## 動作確認（起動後）

ダッシュボードに表示される `web` の URL に対して:

```bash
curl -X POST http://localhost:<port>/api/todos \
  -H 'Content-Type: application/json' -d '{"title":"buy milk"}'

curl http://localhost:<port>/api/todos   # source: mysql → 2回目は valkey
```

`worker` のログにキューから取得・削除したメッセージが出力されます。
