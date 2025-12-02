# MOCHA

Blazor Server (.NET 8) で構築した Microsoft Agent Framework 連携チャット UI。装置エージェント経由で PLC 読み取りやマニュアル検索を行い、ローカルではフェイクエージェントで完結し、認証や外部接続を後から有効化できる。

## できること
- エージェント連携チャット UI（ストリーム表示）。ツール要求を受け取り、装置エージェント経由で PLC 読み取りやマニュアル検索を実行。
- 装置エージェント管理と紐づく履歴フィルタ。サイドバーからエージェントを登録/選択し、その番号ごとに会話を保存。
- 会話履歴とメッセージを SQLite（`chat.db`）へ永続化。初回起動時にスキーマ自動生成。
- ユーザーロール管理（DB 持ち）。`/settings/roles` で Admin が付与/削除、API も提供。
- テーマ・ユーザー設定をブラウザに保存（ライト/ダーク）。
- 認証は Entra ID（Azure AD）と開発用クッキー認証を切替可能。

## セットアップ
1. 前提: .NET 8 SDK。SQLite バイナリは不要（内蔵プロバイダー利用）。
2. 依存復元: `dotnet restore`
3. ローカル起動: `dotnet run --project MOCHA/MOCHA.csproj`  
   - 既定では開発用クッキー認証が有効。`/signup` でメールアドレス+パスワードを登録し、そのままサインイン（既存アカウントは `/login`）。  
   - 起動時に `chat.db` が同ディレクトリに作成される。
4. テスト: `dotnet test`（MSTest ベース、フェイククライアントで外部依存なし）

## 設定ポイント（`MOCHA/appsettings*.json`）
- `ConnectionStrings:ChatDb`: SQLite の場所（既定 `Data Source=chat.db`）。削除すれば再生成。
- `AzureAd`: 本番用 OIDC 認証。`Enabled=true` で有効化し、`TenantId`/`ClientId`/`Domain`/`CallbackPath` をテナント値に置換。
- `Authentication`: 既定スキームの切替。開発時は `DevLogin`、本番は `OpenIdConnect` を設定。
- `DevAuth`: ローカル用クッキー認証。`Enabled=true` なら `/login` でユーザーID/表示名を入力してサインイン。
- `Llm`: Microsoft Agent Framework 用の LLM 設定（OpenAI/Azure OpenAI を切替）。未設定でもフェイクが動作。
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

### エージェント/PLC 設定メモ
- LLM は `Llm` セクションで `Provider`（OpenAI/AzureOpenAI）と `ApiKey`/`Endpoint`/`ModelOrDeployment` を指定。
- PLC 接続情報は装置エージェント設定（DB 永続化）で管理し、`appsettings.json` には置かない想定。PlcAgentTool がエージェント側で処理する。

#### Llm 設定例
OpenAI（ApiKey のみ必須）:
```json
"Llm": {
  "Provider": "OpenAI",
  "ApiKey": "<your-openai-api-key>",
  "ModelOrDeployment": "gpt-5.1-mini",
  "AgentName": "mocha-agent",
  "AgentDescription": "Local dev agent",
  "Instructions": "You are a helpful assistant for MOCHA."
}
```

Azure OpenAI（ApiKey と Endpoint が必須）:
```json
"Llm": {
  "Provider": "AzureOpenAI",
  "Endpoint": "https://<your-resource>.openai.azure.com/",
  "ApiKey": "<your-azure-openai-key>",
  "ModelOrDeployment": "<your-deployment-name>",
  "AgentName": "mocha-agent",
  "AgentDescription": "Local dev agent",
  "Instructions": "You are a helpful assistant for MOCHA."
}
```

## アーキテクチャ概要
- UI: `MOCHA/Components`（チャット画面、サイドバー、ロール設定ページなど）。
- ドメインモデル: `MOCHA/Models`（チャット/ロール/装置エージェント/設定）。
- アプリサービス: `MOCHA/Services`  
  - `Chat`: `ChatOrchestrator` がエージェントのメッセージ/ツールイベントを仲介し、履歴 (`ConversationHistoryState`) とストレージ (`IChatRepository`) を更新。  
  - `AgentChat`: エージェントクライアントの実装/フェイクを DI 切替。  
  - `Agents`: ユーザーごとの装置エージェントを管理し、選択状態を UI に通知。  
  - `Auth`: ユーザーロール永続化とクッキー認証、Admin ブートストラップ。  
  - `Settings`: テーマ/ユーザー設定の保存と適用。
- インフラ: `MOCHA/Data` と `MOCHA/Factories` で SQLite スキーマを初期化し、EF Core の DbContext を提供。

## 画面の使い方（ローカル既定設定）
- サイドバーの「装置エージェント」を登録し、選択してからチャットを送信（エージェントごとに履歴が分かれる）。
- メッセージ入力は `Ctrl+Enter` で送信。Stop ボタンでストリームをキャンセル。
- 履歴はサイドバーに表示され、削除すると会話とメッセージが DB から除去される。
- 右上メニューからテーマ切替、`/settings/roles` で Admin によるロール管理が可能（開発用クッキー認証時は `/login` で選んだユーザーに Admin を付与する設定が利用可）。

## テストと品質
- `dotnet test` でユニットテスト実行（外部サービス不要）。  
  - `FakeChatFlowTests`: フェイクエージェントでツールイベントの流れとオーケストレーションを検証。  
  - `DeviceAgentStateTests`/`ConversationHistoryStateAgentFilterTests`: エージェントと履歴の状態管理を検証。  
  - `DbUserRoleProviderTests`/`RoleBootstrapperTests`: ロール付与・ブートストラップの動作確認。  
  - `UserPreferencesStateTests`: テーマ保存・適用の確認。

## ディレクトリ
- `MOCHA/`: Blazor Server 本体。`Program.cs` で DI とスキーマ初期化を構成。
- `Tests/`: MSTest プロジェクト。
- `docs/`: 設計メモ（`spec.md`、`database-schema.md`）。
