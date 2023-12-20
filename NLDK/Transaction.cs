using Microsoft.EntityFrameworkCore;

namespace NLDK;

public class Transaction : BaseEntity
{
    public string Hash { get; set; }
    public string? BlockHash { get; set; }

    public List<Script> Scripts { get; set; }
    public List<TransactionScript> TransactionScripts { get; set; }

    public object GetKey()
    {
        return Hash;
    }

    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>()
            .HasKey(w => w.Hash);
        modelBuilder.Entity<Transaction>()
            .HasMany(w => w.Scripts)
            .WithMany(script => script.Transactions)
            .UsingEntity<TransactionScript>(builder =>
                    builder
                        .HasOne(ts => ts.Script)
                        .WithMany(script => script.TransactionScripts),
                builder => builder
                    .HasOne(ts => ts.Transaction)
                    .WithMany(transaction => transaction.TransactionScripts));
    }
}