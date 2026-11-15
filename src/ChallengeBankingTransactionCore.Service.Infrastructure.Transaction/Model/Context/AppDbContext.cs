using Microsoft.EntityFrameworkCore;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;

public class AppDbContext : DbContext {
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

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
    }
}
