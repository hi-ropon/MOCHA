# MOCHA

Blazor Server (.NET 8) で構築した Copilot Studio/PLC Gateway 連携チャット UI。ローカルではフェイククライアントで完結し、認証や外部接続を後から有効化できる。

## できること
- Copilot 風チャット UI（ストリーム表示）。ツール要求を受け取り、PLC Gateway へ読み取りを実行（`PlcGateway:Enabled=false` ならフェイクで応答）。
- 装置エージェント管理と紐づく履歴フィルタ。サイドバーからエージェントを登録/選択し、その番号ごとに会話を保存。
- 会話履歴とメッセージを SQLite（`chat.db`）へ永続化。初回起動時にスキーマ自動生成。
- ユーザーロール管理（DB 持ち）。`/settings/roles` で Admin が付与/削除、API も提供。
- テーマ・ユーザー設定をブラウザに保存（ライト/ダーク）。
- 認証は Entra ID（Azure AD）と開発用フェイク認証を切替可能。

## セットアップ
1. 前提: .NET 8 SDK。SQLite バイナリは不要（内蔵プロバイダー利用）。
2. 依存復元: `dotnet restore`
3. ローカル起動: `dotnet run --project MOCHA/MOCHA.csproj`  
   - 既定では `FakeAuth` が有効になり、`dev-user` としてログインした扱い。  
   - 起動時に `chat.db` が同ディレクトリに作成される。
4. テスト: `dotnet test`（MSTest ベース、フェイククライアントで外部依存なし）

## 設定ポイント（`MOCHA/appsettings*.json`）
- `ConnectionStrings:ChatDb`: SQLite の場所（既定 `Data Source=chat.db`）。削除すれば再生成。
- `AzureAd`: 本番用 OIDC 認証。`Enabled=true` で有効化し、`TenantId`/`ClientId`/`Domain`/`CallbackPath` をテナント値に置換。
- `FakeAuth`: ローカル用フェイク認証。`Enabled=true` のままなら認証不要で固定ユーザーとして動作。
- `Copilot`: Copilot Studio 接続設定。`Enabled=false` なら `FakeCopilotChatClient` が応答。
- `PlcGateway`: PLC Gateway への HTTP 呼び出し設定。`Enabled=false` なら `FakePlcGatewayClient` が応答。
- `RoleBootstrap:AdminUserIds`: 起動時に Admin を付与するユーザーID 配列（付与後は空に戻す運用推奨）。

### AzureAd 設定例
```json
"AzureAd": {
  "Enabled": false,
  "Instance": "https://login.microsoftonline.com/",
  "Domain": "your-domain.onmicrosoft.com",
  "TenantId": "00000000-0000-0000-0000-000000000000",
  "ClientId": "00000000-0000-0000-0000-000000000000",
  "CallbackPath": "/signin-oidc"
}
```

### Copilot/PLC 設定メモ
- Copilot へ実接続する場合、`SchemaName` と `EnvironmentId` または `DirectConnectUrl` を設定し、`AccessToken` を適宜付与。
- PLC Gateway は `BaseAddress` と `Timeout` を上書きする。`Enabled=false` のままでもチャット UI の挙動確認は可能。

## アーキテクチャ概要
- UI: `MOCHA/Components`（チャット画面、サイドバー、ロール設定ページなど）。
- ドメインモデル: `MOCHA/Models`（チャット/ロール/装置エージェント/設定）。
- アプリサービス: `MOCHA/Services`  
  - `Chat`: `ChatOrchestrator` が Copilot と PLC Gateway を仲介し、履歴 (`ConversationHistoryState`) とストレージ (`IChatRepository`) を更新。  
  - `Copilot`/`Plc`: 実クライアントとフェイク実装を DI 切替。  
  - `Agents`: ユーザーごとの装置エージェントを管理し、選択状態を UI に通知。  
  - `Auth`: ユーザーロール永続化とフェイク認証、Admin ブートストラップ。  
  - `Settings`: テーマ/ユーザー設定の保存と適用。
- インフラ: `MOCHA/Data` と `MOCHA/Factories` で SQLite スキーマを初期化し、EF Core の DbContext を提供。

## 画面の使い方（ローカル既定設定）
- サイドバーの「装置エージェント」を登録し、選択してからチャットを送信（エージェントごとに履歴が分かれる）。
- メッセージ入力は `Ctrl+Enter` で送信。Stop ボタンでストリームをキャンセル。
- 履歴はサイドバーに表示され、削除すると会話とメッセージが DB から除去される。
- 右上メニューからテーマ切替、`/settings/roles` で Admin によるロール管理が可能（`FakeAuth` では `dev-user` が Admin）。

## テストと品質
- `dotnet test` でユニットテスト実行（外部サービス不要）。  
  - `FakeChatFlowTests`: フェイク Copilot/PLC でオーケストレーションの分岐を検証。  
  - `DeviceAgentStateTests`/`ConversationHistoryStateAgentFilterTests`: エージェントと履歴の状態管理を検証。  
  - `DbUserRoleProviderTests`/`RoleBootstrapperTests`: ロール付与・ブートストラップの動作確認。  
  - `UserPreferencesStateTests`: テーマ保存・適用の確認。

## ディレクトリ
- `MOCHA/`: Blazor Server 本体。`Program.cs` で DI とスキーマ初期化を構成。
- `Tests/`: MSTest プロジェクト。
- `docs/`: 設計メモ（`spec.md`、`database-schema.md`）。
