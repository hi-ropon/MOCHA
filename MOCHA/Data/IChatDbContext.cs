using System.Threading;
using Microsoft.EntityFrameworkCore;
using MOCHA.Services.Chat;

namespace MOCHA.Data;

/// <summary>
/// チャット用の DbContext を抽象化し、テストで差し替えられるようにする。
/// </summary>
public interface IChatDbContext
{
    DbSet<ChatConversationEntity> Conversations { get; }
    DbSet<ChatMessageEntity> Messages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
