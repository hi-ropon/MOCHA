using Microsoft.EntityFrameworkCore;
using MOCHA.Services.Chat;
using MOCHA.Services.Auth;

namespace MOCHA.Data;

public class ChatDbContext : DbContext, IChatDbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    public DbSet<ChatConversationEntity> Conversations => Set<ChatConversationEntity>();
    public DbSet<ChatMessageEntity> Messages => Set<ChatMessageEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ChatConversationEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Title).HasMaxLength(200);
            builder.Property(x => x.UserObjectId).HasMaxLength(200);
            builder.HasMany(x => x.Messages)
                .WithOne(x => x.Conversation)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => new { x.UserObjectId, x.UpdatedAt });
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
    }
}
