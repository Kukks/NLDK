using Microsoft.EntityFrameworkCore;

namespace NLDK;

public class WalletScript : BaseEntity
{
    public string WalletId { get; set; }
    public string ScriptId { get; set; }
    public string? DerivationPath { get; set; }
    public Script Script { get; set; }
    public Wallet Wallet { get; set; }

    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WalletScript>()
            .HasKey(w => new {w.WalletId, w.ScriptId});

        modelBuilder.Entity<WalletScript>()
            .HasOne(w => w.Script)
            .WithMany(script => script.WalletScripts)
            .HasForeignKey(w => w.ScriptId);

        modelBuilder.Entity<WalletScript>()
            .HasOne(w => w.Wallet)
            .WithMany(wallet => wallet.Scripts)
            .HasForeignKey(w => w.WalletId);
    }
}