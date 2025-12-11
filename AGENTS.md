# Repository Guidelines

## ルール
- 日本語で会話すること
- 勝手に `git commit` しないこと。コミットする際は都度許可を取り、コミットメッセージは日本語の現在形で記述する
- DDD（ドメイン駆動設計）と TDD（テスト駆動開発）を採用する
- XMLコメントは体言止めとし、文章末の句読点「。」は付けない

## プロジェクト構成
- `MOCHA/`: Blazor Server (.NET 8) 本体。`Program.cs` で DI を構成し、DDD のドメイン・アプリケーション・インフラを各ディレクトリで分離
- `MOCHA/Components`・`MOCHA/Pages`: Razor UI コンポーネントとページ
- `MOCHA/Services`: チャットオーケストレーション、Agent/Copilot/PLC 統合、Drawing/Manual/Feedback などサービス群
- `MOCHA/Models`: ドメインモデルやストレージオプション
- `MOCHA/Data`: `ChatDbContext`/マイグレーション・インフラ
- `MOCHA.Agents/`: Agent Framework のドメイン・インフラ・ツールセット
- `Tests/`: MSTest のテストプロジェクト（`Tests/MOCHA.Tests.csproj`）。名前空間は本体と一致させている
- `docs/`: 設計メモ（`spec.md`）、図面エージェント、ロール、DB スキーマなど
- `sample/`: プロンプト例・ゲートウェイ応答・図面素材のスタブ
- `wwwroot/`: グローバル CSS/画像

## アーキテクチャ
- `Program.cs` は認証（`DevAuth`/`AzureAd`）、`ChatDbContext`、サービス、`AddMochaAgents` の登録を行い、`IDatabaseInitializer` と `RoleBootstrapper` で起動時の DB 初期化とロール付与を実行
- チャット一連の処理は `IChatOrchestrator`/`IAgentChatClient` を中心に、`ChatStreamEvent` を用いたストリーミングと `ConversationHistoryState` による履歴管理を継続的に行う
- Agent は `MOCHA.Agents` の Organizer → Drawing/PLC/Manual ツールセットで委譲され、`AgentDelegationPolicy` で深さ・許可エッジを制御
- 図面管理は `DrawingRepository`/`DrawingCatalog`/`DrawingContentReader` のトリオで行い、`UserDrawingManualStore` 経由で `ManualToolset`・`ManualAgentTool` に接続
- 設定は `appsettings*.json` で `ConnectionStrings:ChatDb`、`AzureAd`、`DevAuth`、`DrawingStorage`、`PlcStorage`、`Llm`、`AgentDelegation`、`RoleBootstrap` などを保持

## 開発・実行
- 依存復元: `dotnet restore`
- ビルド: `dotnet build`
- 起動: `dotnet run --project MOCHA/MOCHA.csproj`（`DevAuth.Enabled=true` でローカル Cookie 認証、`AzureAd.Enabled=true` で Entra ID）
- `dotnet ef database update --project MOCHA --startup-project MOCHA` で手動マイグレーション同期が可能だが、`IDatabaseInitializer` により起動時に自動適用されるので通常不要
- ブラウザアクセス: `https://localhost:7240` / `http://localhost:5240` で `/signup`/`/login` してサイドバーから装置エージェントを選択しチャットを試す
- UI で `ManualToolset` による `find_manuals`/`read_manual`、`invoke_drawing_agent` の流れを試験し、Stop/Cancel でキャンセルハンドリングを確認する

## テスト方針
- テストプロジェクトは MSTest (`Tests/MOCHA.Tests.csproj`)。テストメソッド名は `メソッド_状態_期待結果` 形式で日本語
- テーマ: ユーザー発話 → `ChatOrchestrator` → Agent ツール/PLC/図面のフロー、`DeviceAgentState`/`DeviceAgentAccessService`、`RoleBootstrapper`、`UserPreferencesState`
- `IAgentChatClient`/`IChatRepository` などはフェイク実装を用いて決定論的なストリーム・ツールレスポンスを検証。`PlcAgentTool` はエージェント側のフェイクを活用
- 図面周りは `DrawingRepository`/`DrawingCatalog`/`DrawingContentReader`/`DrawingRegistrationService` を中心に `Tests/Drawings` で TDD している

## ドキュメント
- `docs/spec.md`：Agent Orchestrator/BFFのシーケンス、UI 方針、認証、テスト戦略
- `docs/drawing-agent.md`：図面エージェントの設計・検索/抽出フロー・テスト項目
- `docs/database-schema.md`：現行 `ChatDbContext` のテーブル・制約・インデックス構成
- `docs/roles.md`：ロールと装置エージェントの割当ポリシー
- `MOCHA.Agents/Resources`：Agent 指示・ツールプロンプトのテンプレート

## セキュリティ・設定ヒント
- 認証情報や API キーは `appsettings.Development.json` または環境変数に設定し、実シークレットをソース管理しないこと
- `Llm` 設定は OpenAI/AzureOpenAI の `Provider`、`ApiKey`、`Endpoint`、`ModelOrDeployment` を指定。未設定時はフェイク Agent が利用できる
- PLC/Gateway 設定は `GatewaySettingEntity`/`PlcUnitEntity` を通じて DB に保存。`FunctionBlockApiClient`/`FunctionBlockService` を介して外部 Gateway を呼び出し、`PlcStorage:RootPath` にファイルを保持
