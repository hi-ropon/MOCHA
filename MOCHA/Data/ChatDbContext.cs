using Microsoft.EntityFrameworkCore;
using MOCHA.Services.Chat;
using MOCHA.Services.Auth;
using MOCHA.Services.Agents;
using MOCHA.Services.Feedback;
using MOCHA.Services.Drawings;
using MOCHA.Services.Architecture;

namespace MOCHA.Data;

/// <summary>
/// チャット機能に必要なエンティティを管理する DbContext
/// </summary>
internal sealed class ChatDbContext : DbContext, IChatDbContext
{
    /// <summary>
    /// DbContext オプション受け取りによる初期化
    /// </summary>
    /// <param name="options">DbContext オプション</param>
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    public DbSet<ChatConversationEntity> Conversations => Set<ChatConversationEntity>();
    public DbSet<ChatMessageEntity> Messages => Set<ChatMessageEntity>();
    public DbSet<ChatAttachmentEntity> Attachments => Set<ChatAttachmentEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<DeviceAgentEntity> DeviceAgents => Set<DeviceAgentEntity>();
    public DbSet<DeviceAgentPermissionEntity> DeviceAgentPermissions => Set<DeviceAgentPermissionEntity>();
    public DbSet<DevUserEntity> DevUsers => Set<DevUserEntity>();
    public DbSet<FeedbackEntity> Feedbacks => Set<FeedbackEntity>();
    public DbSet<DrawingDocumentEntity> Drawings => Set<DrawingDocumentEntity>();
    public DbSet<PcSettingEntity> PcSettings => Set<PcSettingEntity>();
    public DbSet<PlcUnitEntity> PlcUnits => Set<PlcUnitEntity>();
    public DbSet<GatewaySettingEntity> GatewaySettings => Set<GatewaySettingEntity>();
    public DbSet<UnitConfigurationEntity> UnitConfigurations => Set<UnitConfigurationEntity>();

    /// <summary>
    /// エンティティの制約やインデックス構成
    /// </summary>
    /// <param name="modelBuilder">モデルビルダー</param>
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

        modelBuilder.Entity<ChatAttachmentEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.FileName).HasMaxLength(260);
            builder.Property(x => x.ContentType).HasMaxLength(100);
            builder.Property(x => x.UserObjectId).HasMaxLength(200);
            builder.Property(x => x.ConversationId).HasMaxLength(200);
            builder.HasIndex(x => x.MessageId);
            builder.HasOne(x => x.Message)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FeedbackEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ConversationId).HasMaxLength(200);
            builder.Property(x => x.UserObjectId).HasMaxLength(200);
            builder.Property(x => x.Rating).HasMaxLength(20);
            builder.Property(x => x.Comment).HasMaxLength(1000);
            builder.HasIndex(x => new { x.ConversationId, x.MessageIndex, x.UserObjectId }).IsUnique();
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

        modelBuilder.Entity<DeviceAgentPermissionEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserObjectId).HasMaxLength(200);
            builder.Property(x => x.AgentNumber).HasMaxLength(100);
            builder.HasIndex(x => new { x.UserObjectId, x.AgentNumber }).IsUnique();
        });

        modelBuilder.Entity<DevUserEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
            builder.Property(x => x.DisplayName).HasMaxLength(200);
            builder.Property(x => x.PasswordHash).IsRequired();
            builder.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<DrawingDocumentEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserId).HasMaxLength(200);
            builder.Property(x => x.AgentNumber).HasMaxLength(100);
            builder.Property(x => x.FileName).HasMaxLength(260);
            builder.Property(x => x.ContentType).HasMaxLength(200);
            builder.Property(x => x.Description).HasMaxLength(1000);
            builder.Property(x => x.RelativePath).HasMaxLength(500);
            builder.Property(x => x.StorageRoot).HasMaxLength(300);
            builder.HasIndex(x => new { x.UserId, x.AgentNumber, x.CreatedAt });
        });

        modelBuilder.Entity<PcSettingEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserId).HasMaxLength(200);
            builder.Property(x => x.AgentNumber).HasMaxLength(100);
            builder.Property(x => x.Os).HasMaxLength(200);
            builder.Property(x => x.Role).HasMaxLength(200);
            builder.Property(x => x.RepositoryUrlsJson).HasColumnType("TEXT");
            builder.HasIndex(x => new { x.UserId, x.AgentNumber, x.CreatedAt });
        });

        modelBuilder.Entity<PlcUnitEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserId).HasMaxLength(200);
            builder.Property(x => x.AgentNumber).HasMaxLength(100);
            builder.Property(x => x.Name).HasMaxLength(200);
            builder.Property(x => x.Manufacturer).HasMaxLength(100);
            builder.Property(x => x.Model).HasMaxLength(200);
            builder.Property(x => x.Role).HasMaxLength(200);
            builder.Property(x => x.IpAddress).HasMaxLength(100);
            builder.Property(x => x.GatewayHost).HasMaxLength(100);
            builder.Property(x => x.CommentFileJson).HasColumnType("TEXT");
            builder.Property(x => x.ProgramFilesJson).HasColumnType("TEXT");
            builder.Property(x => x.ModulesJson).HasColumnType("TEXT");
            builder.HasIndex(x => new { x.UserId, x.AgentNumber, x.CreatedAt });
        });

        modelBuilder.Entity<GatewaySettingEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserId).HasMaxLength(200);
            builder.Property(x => x.AgentNumber).HasMaxLength(100);
            builder.Property(x => x.Host).HasMaxLength(200);
            builder.HasIndex(x => new { x.UserId, x.AgentNumber });
        });

        modelBuilder.Entity<UnitConfigurationEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserId).HasMaxLength(200);
            builder.Property(x => x.AgentNumber).HasMaxLength(100);
            builder.Property(x => x.Name).HasMaxLength(200);
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.DevicesJson).HasColumnType("TEXT");
            builder.HasIndex(x => new { x.UserId, x.AgentNumber, x.CreatedAt });
        });
    }
}
