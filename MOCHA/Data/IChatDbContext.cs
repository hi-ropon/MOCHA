using System.Threading;
using Microsoft.EntityFrameworkCore;
using MOCHA.Services.Chat;
using MOCHA.Services.Auth;
using MOCHA.Services.Agents;
using MOCHA.Services.Feedback;

namespace MOCHA.Data;

/// <summary>
/// チャット用の DbContext を抽象化し、テストで差し替えられるようにする。
/// </summary>
internal interface IChatDbContext
{
    /// <summary>会話エンティティのセット。</summary>
    DbSet<ChatConversationEntity> Conversations { get; }
    /// <summary>メッセージエンティティのセット。</summary>
    DbSet<ChatMessageEntity> Messages { get; }
    /// <summary>ユーザーロールエンティティのセット。</summary>
    DbSet<UserRoleEntity> UserRoles { get; }
    /// <summary>装置エージェントエンティティのセット。</summary>
    DbSet<DeviceAgentEntity> DeviceAgents { get; }
    /// <summary>装置エージェント利用許可エンティティのセット</summary>
    DbSet<DeviceAgentPermissionEntity> DeviceAgentPermissions { get; }
    /// <summary>フィードバックエンティティのセット</summary>
    DbSet<FeedbackEntity> Feedbacks { get; }
    /// <summary>データベース操作用のファサード。</summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
    /// <summary>
    /// 変更を保存する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>保存されたエントリ数。</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
