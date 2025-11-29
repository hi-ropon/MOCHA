using Microsoft.EntityFrameworkCore;
using MOCHA.Services.Chat;
using MOCHA.Services.Auth;
using MOCHA.Services.Agents;

namespace MOCHA.Data;

/// <summary>
/// チャット機能に必要なエンティティを管理する DbContext。
/// </summary>
internal sealed class ChatDbContext : DbContext, IChatDbContext
{
    /// <summary>
    /// DbContext のオプションを受け取って初期化する。
    /// </summary>
    /// <param name="options">DbContext オプション。</param>
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    public DbSet<ChatConversationEntity> Conversations => Set<ChatConversationEntity>();
    public DbSet<ChatMessageEntity> Messages => Set<ChatMessageEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<DeviceAgentEntity> DeviceAgents => Set<DeviceAgentEntity>();
    public DbSet<DevUserEntity> DevUsers => Set<DevUserEntity>();

    /// <summary>
    /// エンティティの制約やインデックスを構成する。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー。</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ChatConversationEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Title).HasMaxLength(200);
            builder.Property(x => x.UserObjectId).HasMaxLength(200);
            builder.Property(x => x.AgentNumber).HasMaxLength(100);
            builder.HasMany(x => x.Messages)
                .WithOne(x => x.Conversation)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => new { x.UserObjectId, x.UpdatedAt });
            builder.HasIndex(x => new { x.UserObjectId, x.AgentNumber, x.UpdatedAt });
        });

        modelBuilder.Entity<ChatMessageEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Role).HasMaxLength(50);
            builder.Property(x => x.Content).HasMaxLength(4000);
            builder.Property(x => x.UserObjectId).HasMaxLength(200);
            builder.HasIndex(x => x.ConversationId);
            builder.HasIndex(x => new { x.UserObjectId, x.CreatedAt });
        });

        modelBuilder.Entity<UserRoleEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserId).HasMaxLength(200);
            builder.Property(x => x.Role).HasMaxLength(200);
            builder.HasIndex(x => new { x.UserId, x.Role }).IsUnique();
        });

        modelBuilder.Entity<DeviceAgentEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserObjectId).HasMaxLength(200);
            builder.Property(x => x.Number).HasMaxLength(100);
            builder.Property(x => x.Name).HasMaxLength(200);
            builder.HasIndex(x => new { x.UserObjectId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<DevUserEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
            builder.Property(x => x.DisplayName).HasMaxLength(200);
            builder.Property(x => x.PasswordHash).IsRequired();
            builder.HasIndex(x => x.Email).IsUnique();
        });
    }
}
