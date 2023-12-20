using Microsoft.EntityFrameworkCore;

namespace NLDK;

public class ArbitraryData: BaseEntity
{
    public string Key { get; set; }
    public byte[] Value { get; set; }
    
    public string? WalletId { get; set; }
    public Wallet? Wallet { get; set; }
    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArbitraryData>()
            .HasKey(w =>  w.Key);

        modelBuilder.Entity<ArbitraryData>()
            
            .HasOne<Wallet>(data => data.Wallet)
            .WithMany(wallet => wallet.ArbitraryDatas)
            .HasForeignKey(data => data.WalletId);
    }
}