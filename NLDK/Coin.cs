using Microsoft.EntityFrameworkCore;
using NBitcoin;

namespace NLDK;

public class Coin : BaseEntity
{
    public string FundingTransactionHash { get; set; }
    public int FundingTransactionOutputIndex { get; set; }
    public string ScriptId { get; set; }
    public decimal Value { get; set; }
    public string? SpendingTransactionHash { get; set; }
    public int? SpendingTransactionInputIndex { get; set; }

    public List<Channel> Channels { get; set; }
    public Script Script { get; set; }

    public NBitcoin.Coin AsCoin()
    {
        return  new NBitcoin.Coin(new OutPoint(uint256.Parse(FundingTransactionHash), (uint) FundingTransactionOutputIndex), new TxOut(Money.Coins(Value),  NBitcoin.Script.FromHex(ScriptId)));
    }

    public object GetKey()
    {
        return new {FundingTransactionHash, FundingTransactionOutputIndex};
    }

    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Coin>()
            .HasKey(w => new {w.FundingTransactionHash, w.FundingTransactionOutputIndex});
        modelBuilder.Entity<Coin>()
            .HasMany(w => w.Channels)
            .WithOne(channel => channel.Coin)
            .HasForeignKey(channel => new
                {channel.FundingTransactionHash, channel.FundingTransactionOutputIndex});
        modelBuilder.Entity<Coin>()
            .HasOne(w => w.Script)
            .WithMany(script => script.Coins)
            .HasForeignKey(w => w.ScriptId);
    }
}