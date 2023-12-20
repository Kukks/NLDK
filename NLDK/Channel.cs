using Microsoft.EntityFrameworkCore;

namespace NLDK;

public class Channel : BaseEntity
{
    public byte[] Data { get; set; }

    public string FundingTransactionHash { get; set; }
    public int FundingTransactionOutputIndex { get; set; }
    public string WalletId { get; set; }
    public Coin Coin { get; set; }
    public Wallet? Wallet { get; set; }
    
    public byte[]? SpendableData { get; set; }

    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Channel>()
            .HasKey(w => new {w.WalletId, w.FundingTransactionHash, w.FundingTransactionOutputIndex});
        modelBuilder.Entity<Channel>()
            .HasOne(w => w.Coin)
            .WithMany(coin => coin.Channels)
            .HasForeignKey(channel => new
                {channel.FundingTransactionHash, channel.FundingTransactionOutputIndex});
        modelBuilder.Entity<Channel>()
            .HasOne(w => w.Wallet)
            .WithMany(wallet => wallet.Channels)
            .HasForeignKey(w => w.WalletId);
    }
}