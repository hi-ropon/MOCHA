# DrawingAgent

## 目的
- 登録済み図面を装置エージェントに紐づけ、Agent Framework のツール要求から検索・抜粋・要約を自律的に返すための領域。
- 図面ファイル・メタ・検索の責務を BFF 側で分離し、Agent/Manual 層には `drawing:` 形式の参照を渡すことで安全に読取りを委譲する。

## 図面データの永続化とパス設計
- `DrawingDocumentEntity` はユーザー/エージェント/ファイルメタを保持し、`ChatDbContext` の `Drawings` テーブルに保存される。`DrawingRepository` はテーブルの存在チェック・CRUD・エラーハンドリングを担い、`DatabaseErrorDetector` によるマイグレーション遅延を吸収する。
- `DrawingStoragePathBuilder` が `DrawingStorage:RootPath` を基準にタイムスタンプ付きファイル名を生成し、無効文字を `_` に置換するためファイルシステム上の衝突やディレクトリ走査を回避できる。
- `DrawingRegistrationService` は管理者ロール (`UserRoleId.Predefined.Administrator`) を確認し、装置エージェント単位で複数ファイルを保存してテーブルに登録、`Directory.CreateDirectory` と `File.WriteAllBytesAsync` を組み合わせてストレージと DB を同期させる。説明の更新やバリデーションもこのサービスで一元化。

## 検索/読取りの業務ロジック
- `DrawingCatalog` は `IDrawingRepository` から取得したエンティティを `DrawingFile` に変換し、`DrawingStorageOptions.RootPath` と `RelativePath` から存在チェック済みの絶対パスを返す。登録時に `StorageRoot` を override しても対応する。
- `DrawingContentReader` は `DrawingFile` を対象に、プレーンテキスト（`.txt`, `.md` 等）をストリーム直接読取、PDF は `UglyToad.PdfPig` を使ってページ毎にトークンヒット数を計測、スニペットを構成する。最大バイト数/ページ数を制限しつつ `DrawingContentMatch` を返し、ヒットなしなら全文モードに降りる。未対応拡張子はメタメッセージとして扱う。
- `UserDrawingManualStore` は `FileManualStore` に加え `IDrawingRepository` を参照して `ManualHit` を合成する。ファイル名・説明をトークン分割してスコアを加算し、`drawing:{Guid}` 相対パスで検索結果を返す。`ReadDrawingAsync` では `DrawingCatalog` でパス解決し、`DrawingContentReader` 結果を文字列化して `ManualContent` として返す。

## Agent 側の連携
- `ManualToolset` が `find_manuals`/`read_manual` を定義し、`ManualSearchContext` に `UserId`/`AgentNumber`/`LastQuery` を格納して `ManualStore` に渡す。`drawing:` パスは `UserDrawingManualStore` で判別され、`maxBytes` によって PDF の読み取り上限を 10MB に増やす運用になっている。
- `ManualAgentTool` は `ManualToolset` を使い `ManualAgentInstructions` の指示（図面エージェントは `find_manuals` → `read_manual` を必ず呼ぶ）と `contextHint` を組み合わせて `ChatClientAgent` を起動。`invoke_drawing_agent` は `OrganizerToolset` から呼ばれ、`ToolCall`/`AgentEvent` を履歴化しつつ `ManualAgentTool.RunAsync` を実行する。
- `OrganizerToolset` の `BuildDelegationTools` には `invoke_drawing_agent` が含まれ、`AgentDelegationPolicy` の制限（`AgentDelegationOptions:AllowedEdges`）に従って Organizer→Drawing の呼び出し深度を管理。ツール呼び出しの `ToolResult` は `AgentEventFactory.ToolCompleted` で `ChatStreamEvent`/DB に記録される。
- `ManualAgentInstructions` の `Description` は「登録図面の検索・抜粋エージェント」となり、プロンプトではユーザー・エージェント文脈で `find_manuals` → `read_manual` を実行しないと回答しないことや手順（結論→根拠）を明記している。

## テストと TDD
- `Tests/Drawings/DrawingRepositoryTests.cs`：テーブル未作成時の補完、CRUD、例外パスを確認。`DatabaseErrorDetector` を使った再試行フローが検証されている。
- `Tests/Drawings/DrawingStoragePathBuilderTests.cs`：RootPath 解決、タイムスタンプ付きファイル名生成、不正文字置換を保証。
- `Tests/Drawings/DrawingCatalogTests.cs`：存在しないパスのハンドリング、エージェント/ユーザー一致、`DrawingFile.Exists` を返すロジックを網羅。
- `Tests/Drawings/DrawingContentReaderTests.cs`：PDF/テキスト読み取り、トークンヒット、トランケーション、例外フォールバックの Behavior を確認。
- `Tests/Drawings/DrawingRegistrationServiceTests.cs`：管理者権限チェック、最大ファイルサイズ、ファイル保存・登録の全体ワークフローを TDD で通す。

## 設定と補足
- `DrawingStorage:RootPath`（`appsettings*.json`）は相対パス/絶対パスいずれも受け入れ、`DrawingStoragePathBuilder` で基準を解決する。デプロイ時は共有ストレージ（SMB/NFS）でも可。
- `sample/DrawingStorage` などにあるファイルは `ManualAgentTool` のテストや UI 試行時のスタブとして使える。
- `ManualStoreOptions` は `docs/spec.md` に定義された `Manual` ディレクトリを監視し、`UserDrawingManualStore` で図面結果とマージすることで統一的な `find_manuals/read_manual` を提供。
