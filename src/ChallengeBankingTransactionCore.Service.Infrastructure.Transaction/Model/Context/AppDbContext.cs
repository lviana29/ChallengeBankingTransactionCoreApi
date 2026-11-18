using Microsoft.EntityFrameworkCore;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;

public class AppDbContext : DbContext {
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<DailyConsolidatedBalance> DailyConsolidatedBalances { get; set; }
    public DbSet<ProcessedEvent> ProcessedEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity => {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnType("CHAR(36)");
            entity.HasIndex(e => e.AccountNumber).IsUnique();

            entity.Property(e => e.RowVersion)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.CreatedAt)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<TransactionType>(entity => {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnType("CHAR(36)");
        });

        modelBuilder.Entity<Transaction>(entity => {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnType("CHAR(36)");
            entity.Property(e => e.AccountId).HasColumnType("CHAR(36)");
            entity.Property(e => e.TransactionTypeId).HasColumnType("CHAR(36)");

            entity.HasOne(tp => tp.Type)
            .WithMany()
            .HasForeignKey(t => t.TransactionTypeId);

            entity.Property(e => e.IdempotencyKey).HasColumnType("CHAR(36)");
            entity.HasIndex(t => t.IdempotencyKey).IsUnique();

            entity.Property(e => e.CreatedAt)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<OutboxMessage>(entity => {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnType("CHAR(36)");
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Payload).HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(500);
            entity.HasIndex(e => new { e.ProcessedAt, e.RetryCount, e.CreatedAt })
                .HasDatabaseName("IX_OutboxMessage_Pending");
        });

        modelBuilder.Entity<DailyConsolidatedBalance>(entity => {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnType("CHAR(36)");
            entity.Property(e => e.AccountId).HasColumnType("CHAR(36)");
            entity.Property(e => e.AccountNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.BalanceDate).HasColumnType("DATE");
            entity.Property(e => e.TotalCredits).HasColumnType("DECIMAL(18,2)");
            entity.Property(e => e.TotalDebits).HasColumnType("DECIMAL(18,2)");
            entity.Property(e => e.ClosingBalance).HasColumnType("DECIMAL(18,2)");
            entity.HasIndex(e => new { e.AccountId, e.BalanceDate })
                .IsUnique()
                .HasDatabaseName("UQ_DailyBalance_Account_Date");
            entity.HasIndex(e => new { e.AccountNumber, e.BalanceDate })
                .HasDatabaseName("IX_DailyBalance_AccountNumber_Date");
        });

        modelBuilder.Entity<ProcessedEvent>(entity => {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).HasColumnType("CHAR(36)");
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        });
    }
}
