# MOCHA

MOCHAはBlazor Server(.NET 8)上で動作する社内向けチャットBFFで、Microsoft Agent FrameworkとCopilot/PLCツールを仲介しながら会話ストリーム・ツール要求・履歴・認証を一貫管理します。DDDで分離されたドメイン、サービス、インフラが `MOCHA` プロジェクトと `MOCHA.Agents` ライブラリに分担され、TDDで検証しやすい構成を維持しています。

## 特徴
- 複数エージェントを連携するオーケストレータ (`IAgentChatClient`/`IChatOrchestrator`) が Organizer → Drawing → PLC と順次ツールを叩き、ツールログやアクション結果を `ChatStreamEvent` と履歴に反映する。
- 装置エージェント設定（番号・名称・PLC/マニュアル紐づけ）とその権限を `DeviceAgentState`・`DeviceAgentAccessService` で管理し、UI側のサイドバーから選択・履歴フィルタを提供。
- 図面データは `DrawingRepository` ・`DrawingCatalog`・`DrawingContentReader` が `DrawingStorage:RootPath` にあるファイルを参照して `find_drawings`/`read_drawing` を提供する DrawingAgentToolset に引き渡し、OrganizerAgent から `invoke_drawing_agent` で呼び出す。
- チャット履歴・メッセージ・アクション・ロール情報を `ChatDbContext`（PostgreSQL/任意の `ConnectionStrings:ChatDb`）で永続化し、`RoleBootstrapper` が初回起動時の管理者ロールを注入。
- `MOCHA/Components`（ログイン/チャット/ロール画面）と `wwwroot/app.css` のデュオトーンUI＋インタラクティブRPCがストリーミング表示を担い、`UserPreferencesState`/`LocalStorageUserPreferencesStore` がテーマ・表示設定をブラウザに保存。
- `Controllers` 経由でフィードバック・PLC構成・ロール・ユニット設定の REST API を公開し、`FunctionBlockService` が外部ゲートウェイからデバイス構成を取得。
- 認証は `DevAuth` クッキー（デフォルト）と `AzureAd` OpenID Connect を切り替え可能で、いずれも `[Authorize]` を要求して会話にユーザーID/表示名を添付。

## アーキテクチャ
- `MOCHA/Services`: Chat Orchestrator・Agent/Copilot連携・Drawing/Manual/PLCサービス・フィードバック/マークダウンレンダラー・テーマ/ロール管理など、責務ごとにサービスに分離。
- `MOCHA.Agents`: Agent Frameworkのドメイン・インフラ・Application層（Organizer/Drawing/PLCエージェントとツールセット）を定義し、`builder.Services.AddMochaAgents` で登録。
- `MOCHA/Data`: `ChatDbContext`/マイグレーションと `IDatabaseInitializer`（Postgres初期化）により、エンティティとインデックスを保持。
- `MOCHA/Components` + `Pages`: Blazorルート + Razorコンポーネントでログイン、チャット、ロール/設定、テーマ切替を提供。
- `Tests`: MSTestベースで Fake エージェント・リポジトリ・サービスの振る舞いを `Chat`/`Agents`/`Drawings`/`Auth` 等の名前空間構成で網羅。

## セットアップ
1. .NET 8 SDK をインストールし、PostgreSQL等の `ConnectionStrings:ChatDb` に接続可能なDBを用意する。
2. 依存復元: `dotnet restore`（`MOCHA.slnx` がソリューション定義）。
3. データベース: 初回起動時に `IDatabaseInitializer` と EF マイグレーションが自動適用される。必要なら `dotnet ef database update --project MOCHA --startup-project MOCHA` で手動同期。

## 実行
1. 開発用クッキー認証（`DevAuth.Enabled=true`）で起動: `dotnet run --project MOCHA/MOCHA.csproj`
2. `https://localhost:7240`/`http://localhost:5240` にアクセスし、`/signup`/`/login` でユーザーを作成。`/roles` などのUIも同じルートから利用。
3. `AzureAd.Enabled=true` かつ必要なクライアントID等を設定すると Entra ID 認証と Cookie/令牌制御に移行する。`Authentication:DefaultScheme`/`DefaultChallengeScheme` を上書き可能。
4. サイドバーで装置エージェントを選択し、会話入力（`Ctrl+Enter` 送信）→ツール要求→ツール結果/ストリーム応答を確認。Stopボタンでキャンセル。
5. ソリューション全体を指定フォルダへ出力（リリース/発行）する場合は `dotnet publish MOCHA.slnx -c Release -o /path/to/output` を使用。
   - ランタイム込み（self-contained）で公開するには RID（例: `win-x64`/`linux-x64`）と `--self-contained true` を指定し、必要なら出力先も固定する:
     ```
     dotnet publish MOCHA/MOCHA.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
     ```
     `-r` を適切な RID に変更し、`-o` を整理用のフォルダに変更すれば、その実行環境に合わせた実行ファイル一式が得られる。

## 構成設定
- `ConnectionStrings:ChatDb`: PostgreSQL（または任意の `DbContext` 対応）が対象。`Trust Server Certificate` を簡易化用に含めるが、実運用では TLS 証明書を管理。
- `AzureAd`: Entra ID 認証を有効化する場合、`TenantId`/`ClientId`/`Domain`/`CallbackPath` をセット。
- `DevAuth`: ローカル用クッキー認証のパス・Cookie名・有効期限。開発時は `Enabled=true` にする。
- `DrawingStorage:RootPath`/`PlcStorage:RootPath`: 相対または絶対パスで図面・PLCファイルを保持。`DrawingStoragePathBuilder`/`PlcFileStoragePathBuilder` が解決。
- `Llm`: Agent Framework で使う LLM 接続情報（OpenAI/AzureOpenAI）。`ApiKey`・`Endpoint`・`ModelOrDeployment` を設定しつつ、未設定でも Fake が稼働。
- `AgentDelegation`: Organizer→サブエージェントの呼び出し深度（`MaxDepth`）とダイレクトに許可するツールエッジを定義。
- `RoleBootstrap`: 起動時に管理者を付与するユーザーID配列。設定後はクリアしておくのが推奨。

## 開発とテスト
- `dotnet test`（`Tests/MOCHA.Tests.csproj`）で MSTest による全ユニットテスト実行。テスト命名規則は `メソッド_状態_期待結果`、Fake実装による Agent Orchestrator・DrawingCatalog・Role Bootstrapperなどの分岐を網羅。
- `Tests/` 内はDDDに準じて `Chat`/`Agents`/`Drawings`/`Auth`/`Manuals` 等の同名空間に展開されているため、コードと1対1のカバレッジを確保しやすい。
- UI表現を手動確認する場合はブラウザから `dotnet run` 起動後に対話的にフィードバックを送信し、`FeedbackController` で保存されることを確認。
- 追加機能は `MOCHA.Agents` のツールセット拡張と `ChatOrchestrator` のイベント記録を先にテストし、その後 UI/Controller 層を調整するワークフローを推奨。

## 参考資料
- `docs/spec.md`: Agent Orchestrator/BFFのシーケンス・UI方針・認証・テスト戦略。
- `docs/drawing-agent.md`: DrawingAgentおよびツールセット設計。図面検索/読み取りのフローとテスト項目。
- `docs/roles.md`: ロール定義と装置エージェント割り当てポリシー。
- `docs/database-schema.md`: 会話/メッセージ/ロール/装置エージェントのER図とインデックス。
- `MOCHA.Agents/Resources`：Agent応答/ツールロジックで使われるテンプレートやプロンプト。

## 補足
- `wwwroot` には共通スタイルとグローバルリソース。コンポーネント単位の `.razor.css` でスコープ調整を行う。
- `FunctionBlockApiClient`/`FunctionBlockService` は PLCユニット定義を BFF から取得するための仕組みで、`Controllers/FunctionBlocksController.cs` から状態を渡す。
