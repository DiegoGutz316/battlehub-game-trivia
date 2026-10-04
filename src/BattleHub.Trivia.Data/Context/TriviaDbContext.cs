using BattleHub.Trivia.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Trivia.Data.Context;

public sealed class TriviaDbContext(DbContextOptions<TriviaDbContext> options) : DbContext(options)
{
    public DbSet<MatchResultRecord> MatchResults => Set<MatchResultRecord>();

    public DbSet<PlayerResultRecord> PlayerResults => Set<PlayerResultRecord>();

    public DbSet<QuestionRecord> Questions => Set<QuestionRecord>();

    public DbSet<AnswerOptionRecord> AnswerOptions => Set<AnswerOptionRecord>();
    public DbSet<FinishNotificationRecord> FinishNotifications => Set<FinishNotificationRecord>();

    public static void ConfigureSqlServer(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
            sql.MigrationsAssembly(typeof(TriviaDbContext).Assembly.GetName().Name));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinishNotificationRecord>(entity =>
        {
            entity.ToTable("FinishNotifications");
            entity.HasKey(item => item.MatchId);
            entity.Property(item => item.MatchId).HasMaxLength(64);
            entity.HasIndex(item => new { item.Delivered, item.NextAttemptAt });
            entity.HasOne<MatchResultRecord>().WithOne().HasForeignKey<FinishNotificationRecord>(item => item.MatchId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<MatchResultRecord>(entity =>
        {
            entity.ToTable("MatchResults", table =>
                table.HasCheckConstraint("CK_MatchResults_Metadata_IsJson", "ISJSON([Metadata]) = 1"));

            entity.HasKey(match => match.MatchId);
            entity.Property(match => match.MatchId).HasMaxLength(64);
            entity.Property(match => match.GameType).HasMaxLength(32).IsRequired();
            entity.Property(match => match.WinnerUserId).HasMaxLength(64);
            entity.Property(match => match.MetadataJson)
                .HasColumnName("Metadata")
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.HasMany(match => match.Players)
                .WithOne(player => player.Match)
                .HasForeignKey(player => player.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerResultRecord>(entity =>
        {
            entity.ToTable("PlayerResults");
            entity.HasKey(player => new { player.MatchId, player.UserId });
            entity.Property(player => player.MatchId).HasMaxLength(64);
            entity.Property(player => player.UserId).HasMaxLength(64);
            entity.Property(player => player.DisplayName).HasMaxLength(100).IsRequired();
            entity.HasIndex(player => player.UserId);
        });

        modelBuilder.Entity<QuestionRecord>(entity =>
        {
            entity.ToTable("Questions");
            entity.HasKey(question => question.QuestionId);
            entity.Property(question => question.QuestionId).HasMaxLength(64);
            entity.Property(question => question.Text).HasMaxLength(1000).IsRequired();
            entity.Property(question => question.Category).HasMaxLength(100);
            entity.Property(question => question.Difficulty).HasMaxLength(32);
            entity.Property(question => question.CorrectAnswerId).HasMaxLength(64).IsRequired();
            entity.HasIndex(question => question.Category);
            entity.HasIndex(question => question.Difficulty);

            entity.HasMany(question => question.Options)
                .WithOne(option => option.Question)
                .HasForeignKey(option => option.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnswerOptionRecord>(entity =>
        {
            entity.ToTable("AnswerOptions");
            entity.HasKey(option => new { option.QuestionId, option.AnswerId });
            entity.Property(option => option.QuestionId).HasMaxLength(64);
            entity.Property(option => option.AnswerId).HasMaxLength(64);
            entity.Property(option => option.Text).HasMaxLength(500).IsRequired();
        });
    }
}
