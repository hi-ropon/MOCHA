# チャットBFF設計仕様（最新版）

## 目的
- MOCHA は社内向け Blazor Server BFF として Microsoft Agent Framework/Copilot/PLC ツールのハブとなり、ユーザー発話・ツールイベント・履歴・認証を一貫して管理する。
- DDD に基づき `Chat`・`Agents`・`Architecture`・`Drawings` などドメインごとにサービスが分離され、TDD で `Tests/` 以下の対応領域を先に検証できる構造とする。

## 主なユースケース
1. エージェントごとに装置を選択し、Ctrl+Enter でメッセージ送信 → ツール要求/ツール結果/ストリーミング応答をサイドバーとメインで確認。
2. 履歴一覧から会話を再開・削除・タイトル変更し、閲覧中のエージェントと関連づけて保持。サイドバーで `ConversationHistoryState` からフィルタリング。
3. 管理者が `/roles` や `RolesController` でロール/装置エージェントの割当を更新し、`DeviceAgentAccessService` で利用可能なエージェントを制御。
4. 図面/マニュアル/PLC データに対して `ManualToolset`・`DrawingAgent`・`PlcAgentTool` を組み合わせ、質問とツール呼び出しのストリームを `ChatOrchestrator` が履歴保存。
5. `FeedbackController` で応答に評価（Good/Bad）を付与し、低評価リストや会話サマリを比較して改善ポイントを把握。

## チャット送信のシーケンス
1. UI で `UserContext` を構築し、`ConversationHistoryState` と `ConversationSummary` を `ChatOrchestrator` に `UpsertAsync`/`AddMessageAsync` で保存。
2. `IAgentChatClient.SendAsync` に `ChatTurn` を渡し、`AgentDelegationPolicy` に従って Organizer → Drawing/Manual/PLC へ `ToolCall` を委譲。`AgentEventFactory` が `ChatStreamEvent` を生成。
3. `ChatStreamEvent` を受けて `ChatRepository` 側で `ChatMessage`/`ChatAttachment` を永続化。ツール要求・結果・アシスタントメッセージは `activity panel` に反映。
4. `ChatTitleService` がメッセージ内容を要約してタイトル更新リクエストを送信。
5. ストリーム終了時に `assistantBuffer` から最終アシスタントメッセージを DB に保存し、UI に `Completed` イベントを伝播。

## Agent/Tool アーキテクチャ
- `MOCHA.Agents` では OrganizerAgent を起点とし、`OrganizerToolset` が `invoke_iai_agent`/`invoke_oriental_agent`/`invoke_plc_agent`/`invoke_drawing_agent`/`read_plc_gateway` を定義、許可エッジは `AgentDelegationOptions` で指定。
- `DrawingAgent` は `ManualAgentInstructions` で「必ず `find_manuals(agentName=drawingAgent)` → `read_manual`」を実行する指示を含み、`ManualAgentTool` によって `ChatClientAgent` を生成。`RunManualAgentAsync` で `ToolResult` を `AgentEventFactory` 経由で履歴化。
- `ManualToolset` の `find_manuals`/`read_manual` は `IManualStore` を介して `UserDrawingManualStore`（図面との統合）や `FileManualStore` を利用。`drawing:` プレフィックス付き結果は `DrawingContentReader` を使ったメタ・抜粋を返す。
- `PlcAgentTool` および `PlcToolset` は `PlcAgentDataLoader` を使って `PlcUnitEntity`/`FunctionBlock`/コメント/プログラムを `IPlcDataStore` にロードし、Agent から `invoke_plc_agent` や `read_plc_gateway` で参照。
- `IAgentChatClient` は `AgentOrchestratorChatClient`（実運用）と `FakeAgentChatClient`（テスト）の差し替えが可能で、テストではツールイベントを決定論的に注入。

## マニュアル・図面・ファイル管理
- `DrawingRegistrationService` は `IDrawingStoragePathBuilder` で図面ファイルを保存・タイムスタンプ命名し、`DrawingRepository` に CRUD。登録/説明更新は管理者ロールで制限。
- `DrawingCatalog` が `IDrawingRepository` を元に `DrawingFile`（存在フラグ付き）を `ManualStore` に提供し、`DrawingContentReader` がテキスト/PDF からヒット・スニペットを抽出。
- `UserDrawingManualStore` は `ManualSearchContext` を使い、ファイル名・説明のスコアリング＋曖昧マッチ（システムプロンプトに `drawing:` 参照）を行い、UI では `ManualToolset` の `read_manual` で `ManualContent` を受け取る。
- `FunctionBlockService` は `PlcUnitEntity` のファンクションブロックを `FunctionBlocksController` 経由で登録/一覧/削除し、`PlcFileStoragePathBuilder` が CSV を `PlcStorage:RootPath` 以下に保存。
- `PlcAgentDataLoader` はファイルを複数エンコーディングで読み込み、コメント・プログラム・ファンクションブロックを `IPlcDataStore` に注入する。

## データモデリング
- `ChatDbContext`（`MOCHA/Data/ChatDbContext.cs`）は `ChatConversationEntity`、`ChatMessageEntity`、`ChatAttachmentEntity`、`FeedbackEntity`、`UserRoleEntity`、`DeviceAgentEntity`、`DrawingDocumentEntity`、`PlcUnitEntity` などを `OnModelCreating` でインデックス付きに構成し、`docs/database-schema.md` を参照。
- `DeviceAgentState`/`DeviceAgentAccessService` が `DeviceAgentEntity`/`DeviceAgentPermissionEntity` 連携によって装置エージェントの登録・選択・ACL を管理。
- `FeedbackService` は `ChatRepository.GetMessagesAsync` で対象アシスタントメッセージを検証し、`IFeedbackRepository` 経由で `FeedbackController` から追加削除・集計 (`summary`, `recent-bad`, `ratings`) を提供。
- `UnitConfigurationService` は `UnitConfigurationEntity` を `DevicesJson` で構成し、`FunctionBlockService` と `PlcUnitsController` で共有される。

## API/エンドポイント
- `RolesController`：`GET /api/roles/{userId}` でロール一覧、`POST /api/roles/assign`/`DELETE /api/roles/{userId}/{role}` で Administrator 限定のロール管理。
- `FeedbackController`：`POST /api/feedback` で評価登録、`GET /api/feedback/summary/{conversationId}`/`recent-bad`/`ratings/{conversationId}` で集計・再評価。
- `PlcUnitsController`：`GET /api/plc-units?agentNumber=xxx` で PLC ユニット概要。
- `FunctionBlocksController`：`POST /api/plc-units/{plcUnitId}/function-blocks` で CSV アップロード、`GET`/`DELETE` で一覧・削除。`FunctionBlockUploadRequest` は `FunctionBlockDraft` に変換。
- `UnitConfigurationsController`：`GET /api/unit-configurations?agentNumber=xxx`、`POST`/`PUT`/`DELETE` で装置構成の CRUD。

## 認証・認可・設定
- `DevAuth`（`MOCHA/Services/Auth/DevAuthOptions`）はローカル Cookie ログイン。`AzureAd` で OpenID Connect を有効化すると `Microsoft.Identity.Web` に切り替え、いずれも `[Authorize]` を要求。
- `Program.cs` で `RoleBootstrapper` が `RoleBootstrap:AdminUserIds` により初回起動で `Administrator` を注入し、`IUserRoleProvider` と `DbUserRoleProvider` が `UserRoles` テーブルを操作。
- `appsettings*.json` に設定する項目：`ConnectionStrings:ChatDb`、`AzureAd`（Tenant/Client/Callback）、`Authentication:DefaultScheme`、`DevAuth`、`DrawingStorage`/`PlcStorage`、`Llm`（OpenAI/AzureOpenAI）、`AgentDelegation`（MaxDepth/AllowedEdges）、`RoleBootstrap`。
- `FunctionBlockService`/`PlcAgentDataLoader`/`DrawingStoragePathBuilder` などは `IOptions<T>` で `MOCHA.Models` 内のオプションを注入。

## テストポリシー
- フレームワークは MSTest (`Tests/MOCHA.Tests.csproj`)、命名規則は `メソッド_状態_期待結果`（日本語）。`TDD` により失敗するテストを先に書いてから実装。
- `Tests/Chat`：`ChatOrchestrator`/`ChatRepository` の履歴/ストリーム・attachment 保存を検証（`FakeAgentChatClient` を利用）。
- `Tests/Agents`：`AgentDelegationPolicy`、`ManualToolset`、`PlcAgent`、`DrawingAgent` などのツール呼び出しと `AgentEventFactory` の構造。
- `Tests/Drawings`：`DrawingRepository`/`DrawingRegistrationService`/`DrawingCatalog`/`DrawingContentReader`/`DrawingStoragePathBuilder` をウォークスルー。
- `Tests/Architecture`：`FunctionBlockService`/`UnitConfigurationService`/`PlcAgentDataLoader`/`PlcUnitRepository` でファイル・JSON・エンティティの整合性。
- `Tests/Manuals`・`Tests/Markdown` などは `UserDrawingManualStore` や `MarkdownRenderer` の出力を検証しながら TDD を継続。

## 参考資料
- `docs/drawing-agent.md`：図面検索・読み取りフロー、ManualToolset/Agentの連携の詳細。
- `docs/database-schema.md`：最新の `ChatDbContext` テーブル・インデックス。
- `docs/roles.md`：ロール定義・運用メモ。
- `AGENTS.md`：リポジトリルール・構成・セキュリティ。
- `sample/`：`system_prompt.txt`・ゲートウェイ応答のスタブ・図面素材。
