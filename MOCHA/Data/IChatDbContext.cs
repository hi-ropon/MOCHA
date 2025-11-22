using System.Threading;
using Microsoft.EntityFrameworkCore;
using MOCHA.Services.Chat;
using MOCHA.Services.Auth;
using MOCHA.Services.Agents;

namespace MOCHA.Data;

/// <summary>
/// チャット用の DbContext を抽象化し、テストで差し替えられるようにする。
/// </summary>
public interface IChatDbContext
{
    DbSet<ChatConversationEntity> Conversations { get; }
    DbSet<ChatMessageEntity> Messages { get; }
    DbSet<UserRoleEntity> UserRoles { get; }
    DbSet<DeviceAgentEntity> DeviceAgents { get; }
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
