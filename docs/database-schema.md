# データベーステーブル図

以下は SQLite 上で初期化される主要テーブルの構造とリレーションを示した図です。

```mermaid
erDiagram
    Conversations {
        TEXT Id PK "会話ID"
        TEXT UserObjectId "ユーザー ObjectId"
        TEXT Title "会話タイトル"
        TEXT AgentNumber "紐づくエージェント番号 (NULL可)"
        TEXT UpdatedAt "更新日時(ISO文字列)"
    }

    Messages {
        INTEGER Id PK "メッセージID (AUTOINCREMENT)"
        TEXT ConversationId FK "会話ID"
        TEXT UserObjectId "ユーザー ObjectId"
        TEXT Role "role(system/user/assistant等)"
        TEXT Content "本文"
        TEXT CreatedAt "作成日時(ISO文字列)"
    }

    UserRoles {
        INTEGER Id PK "ID (AUTOINCREMENT)"
        TEXT UserId "ユーザーID"
        TEXT Role "ロール名"
        TEXT CreatedAt "作成日時(ISO文字列)"
    }

    DeviceAgents {
        INTEGER Id PK "ID (AUTOINCREMENT)"
        TEXT UserObjectId "ユーザー ObjectId"
        TEXT Number "エージェント番号"
        TEXT Name "エージェント名"
        TEXT CreatedAt "作成日時(ISO文字列)"
    }

    Conversations ||--o{ Messages : "ConversationId"
```

## インデックスとキー
- `Conversations`: `PK(Id)`, `IX_Conversations_UserObjectId_UpdatedAt`, `IX_Conversations_UserObjectId_AgentNumber_UpdatedAt`
- `Messages`: `PK(Id)`, `FK(ConversationId -> Conversations.Id ON DELETE CASCADE)`, `IX_Messages_ConversationId`, `IX_Messages_UserObjectId_CreatedAt`
- `UserRoles`: `PK(Id)`, `UNIQUE(UserId, Role)` (`IX_UserRoles_UserId_Role`)
- `DeviceAgents`: `PK(Id)`, `UNIQUE(UserObjectId, Number)` (`IX_DeviceAgents_UserObjectId_Number`)

## 備考
- `Conversations.AgentNumber` はエージェント紐づけ用の任意列で、`DeviceAgents` とはユーザーごとの `(UserObjectId, Number)` で論理的に対応します（外部キー制約はなし）。
- いずれの日時も ISO 文字列として保存されます（SQLite の TEXT）。
