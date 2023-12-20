using Microsoft.EntityFrameworkCore;

namespace NLDK;

public class Wallet : BaseEntity
{
    public string Id { get; set; }
    public string[] AliasWalletName { get; set; }
    public string Name { get; set; }
    public string Mnemonic { get; set; }
    public string DerivationPath { get; set; }
    public uint LastDerivationIndex { get; set; }
    public string CreationBlockHash { get; set; }
    public List<WalletScript> Scripts { get; } = new();
    public List<Channel> Channels { get; } = new();
    public List<LightningPayment> LightningPayments { get; set; }
    public List<ArbitraryData> ArbitraryDatas { get; set; }
    

    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Wallet>()
            .HasKey(w => w.Id);

        modelBuilder.Entity<Wallet>()
            .HasMany(w => w.Scripts)
            .WithOne(script => script.Wallet)
            .HasForeignKey(script => script.WalletId);


        modelBuilder.Entity<Wallet>()
            .HasMany(w => w.Channels)
            .WithOne(script => script.Wallet)
            .HasForeignKey(coin => coin.WalletId);

        modelBuilder.Entity<Wallet>()
            .HasMany(w => w.LightningPayments)
            .WithOne(script => script.Wallet)
            .HasForeignKey(coin => coin.WalletId);
    }
}