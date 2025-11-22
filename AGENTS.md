# Repository Guidelines

## ルール
- 日本語でやりとりすること
- 勝手にgitでコミットしないこと。コミットするときは毎回私の許可を確認すること。
- DDDを採用すること
- TDDを採用すること

## プロジェクト構成
- `BlazorApp1/`: Blazor Server (.NET 8) 本体。コンポーネント、サービス、スタイルを格納。
- `BlazorApp1/Components/`: Razor UI（ページ、レイアウト、ルート）。
- `BlazorApp1/Services/`: チャットオーケストレーション、Copilot/PLC インターフェース、履歴ステート。
- `BlazorApp1/Models/`: チャットドメインモデルとストリーミングイベント。
- `BlazorApp1/wwwroot/`: 静的アセットと CSS (`app.css`, `bg.png`)。
- `Tests/`: xUnit テストプロジェクト。
- `docs/`: 仕様と設計メモ（`spec.md`）。

## ビルド・テスト・開発コマンド
- `dotnet restore`：NuGet パッケージ復元。
- `dotnet build`：ソリューションをビルド。
- `dotnet run --project BlazorApp1/BlazorApp1.csproj`：ローカル起動（既定 https://localhost:7240 / http://localhost:5240）。
- `dotnet test`：`Tests` 配下の xUnit テストを実行。

## コーディング規約・命名
- C#: インデント4スペース。クラス/メソッドは PascalCase、ローカル/フィールドは camelCase、定数は ALL_CAPS。
- Razor: コンポーネントは薄く保ち、重いロジックはコードビハインド/partial を検討。
- Services/DI: インターフェースは `I` プレフィックス（例 `IChatOrchestrator`）。テスト用フェイクを用意。
- CSS: レイアウト単位はコンポーネントスコープ `.razor.css`、共通調整は `wwwroot/app.css`。

## テスト方針
- フレームワーク: xUnit。対象コードと同じ名前空間構成で `Tests/` に追加。
- 命名: `メソッド_状態_期待結果`。テストメソッド名は日本語にすること
- カバレッジ: ユーザーメッセージフロー、Copilot ツール要求、キャンセル経路などチャットオーケストレーションの分岐を重点。
- 依存: `ICopilotChatClient`, `IPlcGatewayClient` はフェイク/モックで決定論的に。

## コミット・PR ガイド
- コミット: 簡潔な現在形（例 `Add sidebar collapse toggle`, `Refine chat composer UI`）。関連変更はまとめる。
- PR: 目的、主要変更、テスト結果（`dotnet test`、手動UI確認）を明記。課題リンクを付与。UI変更はスクショ/GIF を推奨。

## セキュリティ・設定ヒント
- 認証: MS アカウント認証前提。秘密情報はソースに置かない。ローカルは `appsettings.Development.json` を使用し、実 secrets はコミットしない。
- 外部呼び出し: PLC ゲートウェイと Copilot SDK はインターフェース越し。ベース URL やトークンは設定で管理し、コード直書きしない。
