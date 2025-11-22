# MOCHA (Blazor Server)

## 概要
- .NET 8/Blazor Server のチャット UI。Copilot Studio/PLC ゲートウェイと連携する設計だが、既定はフェイク実装で動作。
- 履歴は SQLite（`chat.db`）に永続化。ユーザー識別は Azure AD 認証の OID を使用。
- 認証はフラグで切替可能（`AzureAd:Enabled`）。ローカル開発は無効のまま動作させられる。

## 必要環境
- .NET 8 SDK
- SQLite はバイナリ不要（`Microsoft.EntityFrameworkCore.Sqlite` で内蔵）
- Azure AD を使う場合はテナント/アプリ登録情報

## セットアップ
1. 依存復元: `dotnet restore`
2. 開発サーバー: `dotnet run --project MOCHA/MOCHA.csproj`
3. テスト: `dotnet test`

## 認証の設定
- `appsettings.json` の `AzureAd:Enabled` を `false` にすると認証なしでアクセス可能（ローカル開発向け、既定）。
- Azure AD で保護する場合は `AzureAd` セクションを実テナント値に更新し、`Enabled` を `true` にする。
  - `TenantId`, `ClientId`, `Domain`, `CallbackPath` を設定。
  - ネットワークから `https://login.microsoftonline.com/{TenantId}/v2.0/.well-known/openid-configuration` に到達できること。

### AzureAd セクション例
```json
"AzureAd": {
  "Enabled": false,                         // true にすると OIDC (Azure AD) 認証を有効化
  "Instance": "https://login.microsoftonline.com/",
  "Domain": "your-domain.onmicrosoft.com",  // アプリ登録テナントのドメイン
  "TenantId": "00000000-0000-0000-0000-000000000000",
  "ClientId": "00000000-0000-0000-0000-000000000000",
  "CallbackPath": "/signin-oidc"            // 既定のサインインコールバック
}
```

## データ永続化
- 接続文字列: `ConnectionStrings:ChatDb`（既定 `Data Source=chat.db`）。`EnsureCreated` で初回起動時に作成。
- `.gitignore` に `chat.db*` を追加済み。必要なら削除して再生成できる。

## 主な設定
- `Copilot`: Copilot Studio 接続設定。未設定/Enabled=false の場合はフェイククライアントで応答。
- `PlcGateway`: `Enabled=true` で HTTP クライアント（`BaseAddress` 既定 `http://localhost:8000`）。`false` ならフェイク。
- `AzureAd`: 上記のとおり認証オン/オフを切替。

### Copilot セクション例
```json
"Copilot": {
  "Enabled": false,          // true で実接続、それ以外はフェイク
  "SchemaName": "",
  "EnvironmentId": "",
  "DirectConnectUrl": "",
  "Cloud": "Prod",
  "AgentType": "Published",
  "CustomPowerPlatformCloud": null,
  "UseExperimentalEndpoint": false,
  "EnableDiagnostics": false,
  "HttpClientName": "CopilotStudio",
  "AccessToken": ""
}
```

## 挙動メモ
- New Chat で初期画面へ戻る。履歴はユーザーごとに SQLite に保存され、サイドバーに表示。
- ツール実行（PLC 読み取り）はフェイク/実クライアントを切替可能。ツール結果後にフェイクの応答を返し、Copilot 側のフェイク応答も表示する。
