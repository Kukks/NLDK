using BTCPayServer.Lightning;
using Microsoft.EntityFrameworkCore;

namespace NLDK;

public class LightningPayment: BaseEntity
{
    public string PaymentHash { get; set; }
    public string? PaymentId { get; set; }
    public string? Preimage { get; set; }
    public string? Secret { get; set; }
    public string WalletId { get; set; }
    public bool Inbound { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public long Value { get; set; }
    public Wallet Wallet { get; set; }
    public LightningPaymentStatus Status { get; set; }


    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LightningPayment>()
            .HasKey(w => new {w.WalletId, w.PaymentHash, w.Inbound});
        modelBuilder.Entity<LightningPayment>()
            .HasOne(w => w.Wallet)
            .WithMany(wallet => wallet.LightningPayments)
            .HasForeignKey(w => w.WalletId);
    }
}