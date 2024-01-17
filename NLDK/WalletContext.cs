using Microsoft.EntityFrameworkCore;

namespace NLDK;

public class WalletContext : DbContext
{
    public DbSet<Channel> Channels { get; set; }
    public DbSet<Coin> Coins { get; set; }
    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<TransactionScript> TransactionScripts { get; set; }
    public DbSet<WalletScript> WalletScripts { get; set; }
    public DbSet<Script> Scripts { get; set; }
    public DbSet<LightningPayment> LightningPayments { get; set; }
    public DbSet<ArbitraryData> ArbitraryData { get; set; }

    // public string DbPath { get; }

    public WalletContext()
    {
        
    }
    
    override protected void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if(!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite("Data Source=wallet.db");
    }
    public WalletContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var models = modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(BaseEntity).IsAssignableFrom(e.ClrType)).ToArray();
        foreach (var entityType in models )
        {
            var method = entityType.ClrType.GetMethod(nameof(BaseEntity.OnModelCreating));
            method?.Invoke(null, new object[] {modelBuilder});
        }
    }
}