# DrawingAgent 設計メモ

## 目的と前提
- 装置エージェントに登録した図面を DrawingAgent というサブエージェントで扱い、OrganizerAgent から `invoke_drawing_agent` で振り分けられるようにする。
- 図面ファイルは設定 `DrawingStorage:RootPath` をルートとする実ファイルを参照する。開発環境では `MOCHA/DrawingStorage`（例: `D:\dev\MOCHA\MOCHA\DrawingStorage`）をそのまま読む。

## ドメイン/コンポーネント
- DrawingCatalog（新規サービス）  
  - 依存: `IDrawingRepository`, `DrawingStorageOptions`  
  - 責務: エージェント番号・ユーザー ID で図面メタを取得し、`RelativePath` + `RootPath` からファイル存在を確認。ファイルサイズ/拡張子から対応可否を判定。
- DrawingContentReader（新規）  
  - 依存: `DrawingStorageOptions`  
  - 責務: 図面ファイルをテキスト化（当面は PDF のテキスト抽出と先頭ページのサマリに限定）。読めない形式はメタ情報だけ返す。
- DrawingAgentToolset（新規）  
  - ツール:  
    - `find_drawings(agentNumber, query)` → 図面候補一覧（タイトル/説明/拡張子/サイズ/最終更新/プレビュー可否）  
    - `read_drawing(drawingId, page?, maxBytes?)` → 先頭ページ or 指定ページのテキストサマリ + ファイルパス（相対）  
  - イベント: `ToolRequested/ToolCompleted` を既存ログに流す。
- DrawingAgent（新規 ChatClientAgent）  
  - instructions: 「図面の場所/概要を答える。読めない形式はメタ情報のみ返す。引用に `drawing:{id}` を残す」  
  - tools: DrawingAgentToolset のみを持ち、回答は日本語で簡潔に返す。
- OrganizerToolset 拡張  
  - `invoke_drawing_agent(question, agentNumber?)` を追加。Organizer は図面関連キーワード（図面/寸法/型番/リビジョン等）でこのツールを優先選択。

## フロー
1. UI から図面に関する質問を受信。
2. ChatOrchestrator → OrganizerAgent。Organizer は `invoke_drawing_agent` を選択し、ToolRequested/Started を履歴に記録。
3. DrawingAgent が `find_drawings` で候補を出し、必要に応じて `read_drawing` で抜粋を取得。ファイルは `DrawingStorageOptions.RootPath` + `RelativePath` を開く。
4. DrawingAgent が応答をまとめ、OrganizerAgent が最終回答をストリーム。ActionName は `invoke_drawing_agent` として保存。

## 設定/ストレージの扱い
- `DrawingStorage:RootPath` に絶対パスを設定するとそのまま利用可能（例: `D:\\dev\\MOCHA\\MOCHA\\DrawingStorage`）。設定が相対の場合はアプリ基準で解決。
- 図面保存時に `RelativePath` を必ず保存する想定。既存 `DrawingStoragePathBuilder` を流用。
- 読み取り不可/不在の場合はエラーでなく警告文を返し、メタ情報のみ回答する。

## TDD 観点のテスト項目（追加予定）
- DrawingCatalog: `RootPath` + `RelativePath` 解決、存在しない場合は null を返すこと。
- DrawingContentReader: PDF からのテキスト抽出、サイズ上限超過時の打ち切り、非対応拡張子でのフォールバック。
- DrawingAgentToolset: `find_drawings` が agentNumber と userId でスコープされること、`read_drawing` がイベントを発火し Action を残すこと。
- OrganizerToolset: 図面関連キーワードで `invoke_drawing_agent` を選択し、ActionName が保存されること（フェイク DrawingAgent で検証）。

## 実装ステップ案
1. ドメイン/サービス追加: DrawingCatalog, DrawingContentReader + ユニットテスト。
2. DrawingAgentToolset と DrawingAgent（Fake/実装）を追加し、OrganizerToolset に `invoke_drawing_agent` を登録。
3. ChatOrchestrator テストを拡張し、図面ツールの Action ログとストリームイベントを検証。
4. UI: ツールログに DrawingAgent 呼び出しを表示（必要ならラベル追加）。
