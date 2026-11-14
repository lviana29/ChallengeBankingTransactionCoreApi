using Microsoft.EntityFrameworkCore;
using ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Base;

namespace ChallengeBankingTransactionCore.Service.Infrastructure.Transaction.Model.Context;

public class AppDbContext : DbContext {
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transaction>(a => {
            a.HasOne(ty => ty.Type)
            .WithOne()
            .HasForeignKey<TransactionType>(t => t.Id);
            a.HasIndex(t => t.TransactionTypeId).IsUnique();
            a.HasIndex(a => a.AccountId).IsUnique();
        });
    }

}
