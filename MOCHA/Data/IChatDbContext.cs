using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MOCHA.Services.Chat;
using MOCHA.Services.Auth;
using MOCHA.Services.Agents;
using MOCHA.Services.Feedback;
using MOCHA.Services.Drawings;
using MOCHA.Services.Architecture;

namespace MOCHA.Data;

/// <summary>
/// チャット用 DbContext の抽象化とテスト差し替え
/// </summary>
internal interface IChatDbContext
{
    /// <summary>会話エンティティのセット</summary>
    DbSet<ChatConversationEntity> Conversations { get; }
    /// <summary>メッセージエンティティのセット</summary>
    DbSet<ChatMessageEntity> Messages { get; }
    /// <summary>添付エンティティのセット</summary>
    DbSet<ChatAttachmentEntity> Attachments { get; }
    /// <summary>ユーザーロールエンティティのセット</summary>
    DbSet<UserRoleEntity> UserRoles { get; }
    /// <summary>装置エージェントエンティティのセット</summary>
    DbSet<DeviceAgentEntity> DeviceAgents { get; }
    /// <summary>装置エージェント利用許可エンティティのセット</summary>
    DbSet<DeviceAgentPermissionEntity> DeviceAgentPermissions { get; }
    /// <summary>フィードバックエンティティのセット</summary>
    DbSet<FeedbackEntity> Feedbacks { get; }
    /// <summary>図面エンティティのセット</summary>
    DbSet<DrawingDocumentEntity> Drawings { get; }
    /// <summary>PC設定エンティティのセット</summary>
    DbSet<PcSettingEntity> PcSettings { get; }
    /// <summary>PLCユニットエンティティのセット</summary>
    DbSet<PlcUnitEntity> PlcUnits { get; }
    /// <summary>ゲートウェイ設定エンティティのセット</summary>
    DbSet<GatewaySettingEntity> GatewaySettings { get; }
    /// <summary>装置ユニット構成エンティティのセット</summary>
    DbSet<UnitConfigurationEntity> UnitConfigurations { get; }
    /// <summary>サブエージェント設定エンティティのセット</summary>
    DbSet<AgentDelegationSettingEntity> AgentDelegationSettings { get; }
    /// <summary>トラッキング操作へのアクセス</summary>
    ChangeTracker ChangeTracker { get; }
    /// <summary>データベース操作用ファサード</summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
    /// <summary>
    /// 変更保存
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>保存されたエントリ数</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
