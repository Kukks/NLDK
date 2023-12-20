using Microsoft.EntityFrameworkCore;

namespace NLDK;

public class Script : BaseEntity
{
    public string Id { get; set; }

    public List<Transaction> Transactions { get; set; }
    public List<WalletScript> WalletScripts { get; set; }
    public List<Coin> Coins { get; set; }
    public List<TransactionScript>? TransactionScripts { get; set; }

    public object GetKey()
    {
        return Id;
    }

    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Script>()
            .HasKey(w => w.Id);
        modelBuilder.Entity<Script>()
            .HasMany(w => w.Transactions)
            .WithMany(transaction => transaction.Scripts);
        modelBuilder.Entity<Script>()
            .HasMany<WalletScript>(script => script.WalletScripts)
            .WithOne(ws => ws.Script)
            .HasForeignKey(ws => ws.ScriptId);
        modelBuilder.Entity<Script>()
            .HasMany<Coin>(script => script.Coins)
            .WithOne(coin => coin.Script);
    }
    
    public NBitcoin.Script ToScript()
    {
        return NBitcoin.Script.FromHex(Id);
    } 
}