# チャットアプリ設計仕様（ドラフト）

## 目的
- ChatGPTライクな社内チャットUIで Copilot Studio エージェントと連携し、PLC Gateway 経由で装置値を取得する。
- 認証は社内 Entra ID シングルテナント。会話データは SQLite に保存し、ユーザーの同意を得て分析用途に活用する。

## ユースケース
- UC1: ユーザーがチャットする（ストリーミング表示、ツール実行ログを含む）
- UC2: ユーザーがチャット履歴を確認する（検索/フィルタ、再開）

## シーケンス（整理版）
1. UI→BFF: ユーザー発話送信。
2. BFF→Copilot SDK: send message。
3. Copilot SDK→BFF: アクション要求（例: read_device）。
4. BFF→PLC Gateway: REST 呼び出し（別ホスト `http://<pc>:8000/api/...`）。
5. BFF→Copilot SDK: アクション結果 submit。
6. Copilot SDK→BFF: 生成応答をストリーム。
7. BFF→UI: ストリーミング配信。履歴は SQLite に即時書き込み。

## UIデザイン方針
- ルック: デュオトーングラデ背景（深緑〜紺）+ 微ノイズ、ガラス質カード。主色はエメラルド1色。
- レイアウト: ヘッダー（環境/接続バッジ）、左サイドバー（履歴＋検索/フィルタ）、メイン（メッセージリスト＋ツール実行ログ）、下部入力（多段行、Ctrl+Enter送信、ショートカットチップ）。
- レスポンシブ: 768px 以下でサイドバーをオーバーレイ。メッセージは1カラム。
- アクセシビリティ: 高コントラスト切替、live regionで新着通知。

## 認証/認可
- Entra ID シングルテナント (`Microsoft.Identity.Web`/Cookie)。
- すべてのページ/APIを `[Authorize]`。ユーザーID/表示名を会話メタに記録。
- Gateway は BFF 経由のみ到達可能にし、CORS を閉じる想定（社内LAN）。

## データモデル（SQLite）
- Conversations: Id, UserId, Title, CreatedAt, UpdatedAt, CopilotConversationId。
- Messages: Id, ConversationId, Role(User/Assistant/System/Tool), Content, CreatedAt, CorrelationId。
- Actions: Id, ConversationId, ActionName, PayloadJson, ResultJson, Status, LatencyMs, CreatedAt。
- UserConsent: UserId, ConsentedAt, Version。

## インターフェース（テスト容易化のため抽象化）
- `ICopilotChatClient`  
  - `Task<IAsyncEnumerable<ChatStreamEvent>> SendAsync(ChatTurn turn, CancellationToken ct)`  
  - `Task SubmitActionResultAsync(string conversationId, CopilotActionResult result, CancellationToken ct)`  
- `IPlcGatewayClient`  
  - `Task<PlcReadResult> ReadAsync(PlcReadRequest req, CancellationToken ct)`  
  - `Task<PlcBatchReadResult> BatchReadAsync(PlcBatchReadRequest req, CancellationToken ct)`
- `IChatOrchestrator`  
  - `Task<IAsyncEnumerable<ChatStreamEvent>> HandleUserMessageAsync(UserContext user, string conversationId, string text, CancellationToken ct)`  
  - 内部で Copilot アクション要求を検知し、PlcGateway を叩いて結果を返す。
- 上記を DI で差し替え可能にし、テストでは Fake 実装を挿入して Copilot/Gateway なしで検証できるようにする。

## API/BFF（例）
- `POST /api/chat/send` → 入力を送信しストリームIDを返す。
- `GET /api/chat/stream/{id}` → Server-Sent Events/SignalR でストリーミング。
- `GET /api/history` / `GET /api/history/{id}` → 履歴一覧/詳細。
- `POST /api/history/{id}/title` → タイトル付け。
- `POST /api/history/{id}/delete` → 削除（ユーザーリクエスト時）。
- `POST /api/consent` → 同意記録。

## テスト戦略
- Copilot/Gateway を実接続せず、Fake 実装を注入して ChatOrchestrator のハンドリングをユニットテスト。  
  - シナリオ: (1) アクションなしの通常応答、(2) read_device 要求→擬似 Gateway 応答→最終応答。
- DI 登録を `IServiceCollection` 経由で切替可能にする（`UseFakeIntegrations` オプション）。

## 未決定/今後の確認
- Copilot SDK の正式 API 名称/パッケージ（最新ドキュメントを確認し反映）。
- Gateway 接続先ホストの命名規則（PC名/IP）と TLS 有無。
- 履歴保持ポリシー文面（ユーザー同意テキスト）。
