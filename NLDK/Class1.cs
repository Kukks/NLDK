using AsyncKeyedLock;
using BTCPayServer.Lightning;
using Microsoft.EntityFrameworkCore;
using NBitcoin;
using NBXplorer.Models;
using org.ldk.util;

namespace NLDK;

public interface BaseEntity
{
    static abstract void OnModelCreating(ModelBuilder modelBuilder);
}

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
            .HasKey(w => new {w.WalletId, w.PaymentHash, w.Inbound, w.PaymentId});
        modelBuilder.Entity<LightningPayment>()
            .HasOne(w => w.Wallet)
            .WithMany(wallet => wallet.LightningPayments)
            .HasForeignKey(w => w.WalletId);
    }
}

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

public class TransactionScript
{
    public string TransactionHash { get; set; }
    public string ScriptId { get; set; }
    public bool Spent { get; set; }

    public Transaction Transaction { get; set; }
    public Script Script { get; set; }
}

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

    // public string DbPath { get; }

    public WalletContext()
    {
        
    }
    
    override protected void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
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

public class WalletService
{
    private readonly IDbContextFactory<WalletContext> _dbContextFactory;
    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    private readonly Network _network;

    public WalletService(IDbContextFactory<WalletContext> dbContextFactory, AsyncKeyedLocker<string> asyncKeyedLocker, Network network)
    {
        _dbContextFactory = dbContextFactory;
        _asyncKeyedLocker = asyncKeyedLocker;
        _network = network;
    }

    private IQueryable<Wallet> WalletQueryable(DbSet<Wallet> wallets)
    {
        return wallets.Include(wallet => wallet.Scripts)
            .ThenInclude<Wallet, WalletScript, Script>(script => script.Script)
            .ThenInclude(script => script.Coins)
            .Include(wallet => wallet.Scripts)
            .ThenInclude<Wallet, WalletScript, Script>(script => script.Script)
            .ThenInclude(script => script.TransactionScripts)
            .Include(wallet => wallet.Scripts)
            .ThenInclude<Wallet, WalletScript, Script>(script => script.Script)
            .ThenInclude(script => script.Transactions)
            .Include(wallet => wallet.Channels);
    }

    public async Task<Wallet?> Get(string walletId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await WalletQueryable(context.Wallets).FirstOrDefaultAsync(
            wallet => wallet.Id == walletId || wallet.AliasWalletName.Contains(walletId), cancellationToken);
    }  
    public async Task<List<Wallet>> GetAll(CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await WalletQueryable(context.Wallets).ToListAsync(cancellationToken);
    }

    public async Task<string> Create(Mnemonic mnemonic, string? name, string derivationPath, string currentBlockHash, string[] aliases,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var id = mnemonic.DeriveExtKey().GetPublicKey().GetHDFingerPrint().ToString();
        var wallet = new Wallet
        {
            Id =id,
            Name = name??id,
            Mnemonic = mnemonic.ToString(),
            DerivationPath = derivationPath,
            LastDerivationIndex = 0,
            CreationBlockHash = currentBlockHash,
            AliasWalletName = aliases
        };
        await context.Wallets.AddAsync(wallet, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return wallet.Id;
    }

    public async Task<Script> DeriveScript(string walletId, CancellationToken cancellationToken = default)
    {
        using var releaser = await _asyncKeyedLocker.LockAsync(walletId, cancellationToken);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var wallet = await context.Wallets.FindAsync(walletId);
        if (wallet is null)
            throw new ArgumentException("Wallet not found", nameof(walletId));
        wallet.LastDerivationIndex++;
        var derivationPath = wallet.DerivationPath.Replace("*", wallet.LastDerivationIndex.ToString());
        var key = new Mnemonic(wallet.Mnemonic)
            .DeriveExtKey()
            .Derive(KeyPath.Parse(derivationPath));
        var newScript = key
            .GetPublicKey()
            .GetAddress(ScriptPubKeyType.Segwit, _network);

        var script = new Script()
        {
            Id = newScript.ScriptPubKey.ToHex(),
        };
        var walletScript = new WalletScript()
        {
            ScriptId = script.Id,
            WalletId = walletId,
            DerivationPath = derivationPath
        };
        await context.Scripts.Upsert(script).NoUpdate().RunAsync(cancellationToken);
        await context.WalletScripts.Upsert(walletScript).NoUpdate().RunAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return script;
    }

    private async Task OnTransactionReplaced(string txHash, WalletContext context,
        CancellationToken cancellationToken = default)
    {
        var coinsToRemove =
            await context.Coins.Include(coin => coin.Channels).Where(coin => coin.FundingTransactionHash == txHash)
                .ToListAsync(cancellationToken: cancellationToken);
        var txToRemove = context.Transactions.Include(transaction => transaction.TransactionScripts).Where(tx => tx.Hash == txHash);
        var channelToRemove = coinsToRemove.SelectMany(coin => coin.Channels).Select(channel => channel!).ToArray();

        context.Coins.RemoveRange(coinsToRemove);
        context.Transactions.RemoveRange(txToRemove);
        context.TransactionScripts.RemoveRange(txToRemove.SelectMany(transaction => transaction.TransactionScripts));
        context.Channels.RemoveRange(channelToRemove);

        await context.Coins.Where(coin => coin.SpendingTransactionHash == txHash)
            .ForEachAsync(coin =>
            {
                coin.SpendingTransactionHash = null;
                coin.SpendingTransactionInputIndex = null;
            }, cancellationToken: cancellationToken);
    }
    

    
    public async Task OnTransactionSeen(Wallet wallet, TransactionInformation transaction,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
       
        var tx = new Transaction()
        {
            Hash = transaction.TransactionId.ToString(),
            BlockHash = transaction.BlockHash?.ToString()
        };
        await context.Transactions.Upsert(tx).WhenMatched((transaction1, transaction2) => new Transaction()
        {
            BlockHash = transaction2.BlockHash,
            Hash = transaction2.Hash
        }).RunAsync(cancellationToken);
        if (transaction.Replacing is not null || transaction.Replacing == uint256.Zero)
        {
            await OnTransactionReplaced(transaction.Replacing.ToString(), context, cancellationToken);
        }

        var outScripts = transaction.Outputs.Select(o => o.ScriptPubKey).Select(script1 => script1.ToHex()).ToArray();
        var inScripts = transaction.Inputs.Select(i => i.ScriptPubKey).Select(script1 => script1.ToHex()).ToArray();
        var ins = transaction.Inputs.Select(i => (i, transaction.Transaction.Inputs[i.InputIndex])).ToArray();
        var scriptsToMatch = outScripts.Concat(inScripts).Distinct().ToArray();

        await context.Scripts.UpsertRange(scriptsToMatch.Select(script => new Script()
        {
            Id = script,
        })).NoUpdate().RunAsync(cancellationToken);

        await context.WalletScripts.UpsertRange(transaction.Outputs.Select(output => new WalletScript()
        {
            WalletId = wallet.Id,
            ScriptId = output.ScriptPubKey.ToHex(),
            DerivationPath = wallet.DerivationPath.Replace("*", output.KeyPath.ToString()),
        })).NoUpdate().RunAsync(cancellationToken);

        await context.TransactionScripts.UpsertRange(transaction.Inputs.Select(input => new TransactionScript()
        {
            ScriptId = input.ScriptPubKey.ToHex(),
            TransactionHash = transaction.TransactionId.ToString(),
            Spent = true
        }).Concat(transaction.Outputs.Select(input => new TransactionScript()
        {
            ScriptId = input.ScriptPubKey.ToHex(),
            TransactionHash = transaction.TransactionId.ToString(),
            Spent = false
        }))).NoUpdate().RunAsync(cancellationToken);

        await context.Coins.UpsertRange(transaction.Outputs.Select(transactionOutput => new Coin()
        {
            ScriptId = transactionOutput.ScriptPubKey.ToHex(),
            FundingTransactionHash = transaction.TransactionId.ToString(),
            FundingTransactionOutputIndex = transactionOutput.Index,
            Value = ((Money) transactionOutput.Value).ToDecimal(MoneyUnit.BTC)
        })).NoUpdate().RunAsync(cancellationToken);

        await context.Coins.UpsertRange(ins.Select(tuple => new Coin()
        {
            FundingTransactionHash = tuple.Item2.PrevOut.Hash.ToString(),
            FundingTransactionOutputIndex = (int) tuple.Item2.PrevOut.N,
            ScriptId = tuple.Item1.ScriptPubKey.ToHex(),
            SpendingTransactionHash = transaction.TransactionId.ToString(),
            SpendingTransactionInputIndex = tuple.i.InputIndex,
            Value = ((Money) tuple.i.Value).ToDecimal(MoneyUnit.BTC),
        })).WhenMatched((coin, coin1) => new Coin()
        {
            SpendingTransactionHash = coin1.SpendingTransactionHash,
            SpendingTransactionInputIndex = coin1.SpendingTransactionInputIndex
        }).RunAsync(cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task HandlePendingTxs(Func<List<Transaction>, Task> process, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var txs = await context.Transactions.Where(transaction => transaction.BlockHash == null).ToListAsync(cancellationToken: cancellationToken);
        await process.Invoke(txs);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddOrUpdateChannel(Channel channel, CancellationToken cancellationToken = default)
    {
        
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.Channels.Upsert(channel).RunAsync(cancellationToken);
    }

    public async Task TrackScript(string walletId, NBitcoin.Script script, CancellationToken cancellationToken = default)
    {
        
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await context.Scripts.Upsert(new Script()
        {
            Id = script.ToHex(),

        }).NoUpdate().RunAsync(cancellationToken);
        await context.WalletScripts.Upsert(new WalletScript()
        {
            ScriptId = script.ToHex(),
            WalletId = walletId,
            DerivationPath = null

        }).NoUpdate().RunAsync(cancellationToken);
        
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddSpendableToCoin(string walletId, (OutPoint outpoint, TxOut txOut, byte[] write)[] set, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
       await context.Scripts.UpsertRange(set.Select( tuple => new Script()
       {
           Id = tuple.txOut.ScriptPubKey.ToHex()
       })).NoUpdate().RunAsync(cancellationToken);
       
        await context.WalletScripts.UpsertRange(set.Select(tuple => new WalletScript()
        {
            ScriptId = tuple.txOut.ScriptPubKey.ToHex(),
            WalletId = walletId,
            DerivationPath = null

        })).NoUpdate().RunAsync(cancellationToken);

        await context.Coins.UpsertRange(set.Select(tuple => new Coin()
        {
            FundingTransactionHash = tuple.outpoint.Hash.ToString(),
            FundingTransactionOutputIndex = (int) tuple.outpoint.N,
            ScriptId = tuple.txOut.ScriptPubKey.ToHex(),
            Value = tuple.txOut.Value.ToDecimal(MoneyUnit.BTC),
        })).NoUpdate().RunAsync(cancellationToken);
        
        
        await context.Channels.Include(channel => channel.Coin).Where(channel =>
                channel.WalletId == walletId && set.Any(tuple => tuple.outpoint.Hash.ToString() == channel.Coin.SpendingTransactionHash && tuple.outpoint.N == channel.Coin.SpendingTransactionInputIndex))
            .ForEachAsync(channel =>
            {
                channel.SpendableData = set.First(tuple => tuple.outpoint.Hash.ToString() == channel.Coin.SpendingTransactionHash && tuple.outpoint.N == channel.Coin.SpendingTransactionInputIndex).write;
            }, cancellationToken: cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Payment(LightningPayment lightningPayment, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.LightningPayments.Upsert(lightningPayment).RunAsync(cancellationToken);
    }

    public async Task<NBitcoin.Transaction> CreateTransaction(string walletId, List<TxOut> txOuts, FeeRate feeRate, CancellationToken cancellationToken = default)
    {
        
        var txBuilder = _network.CreateTransactionBuilder();
        var script = await DeriveScript(walletId, cancellationToken);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        //fetch all coins that are not spent associated with this wallet
        var coins = await context.Coins.Include(coin => coin.Script).ThenInclude(script1 => script1.WalletScripts).Where(coin => coin.Script.WalletScripts.Any(walletScript => walletScript.WalletId == walletId && walletScript.DerivationPath != null && coin.SpendingTransactionHash == null)).ToListAsync(cancellationToken: cancellationToken);
        //group coins by walletscript
        var coinsByWalletScript = coins.GroupBy(coin => coin.Script.WalletScripts.First(walletScript => walletScript.WalletId == walletId && walletScript.DerivationPath != null)).ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());
        
        var wallet = await context.Wallets.FindAsync(walletId);
        
        var mnemonic = new Mnemonic(wallet!.Mnemonic).DeriveExtKey()!;
        txBuilder = coinsByWalletScript.Aggregate(txBuilder, (current, keysAndCoin) => current.AddKeys(mnemonic.Derive(KeyPath.Parse(keysAndCoin.Key.DerivationPath))).AddCoins(keysAndCoin.Value.Select(coin => new NBitcoin.Coin(uint256.Parse(coin.FundingTransactionHash), (uint) coin.FundingTransactionOutputIndex, Money.Coins(coin.Value), NBitcoin.Script.FromHex(coin.ScriptId)))));
        txBuilder = txOuts.Aggregate(txBuilder, (current, c )=> current.Send(c.ScriptPubKey, c.Value));   
        return txBuilder
            .SetChange(NBitcoin.Script.FromHex(script.Id))
            .SendEstimatedFees(feeRate).BuildTransaction(true);
    }

    public async Task PaymentUpdate( string walletId, string paymentHssh, bool inbound, string paymentId, bool failure, string? preimage, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var payment = await context.LightningPayments.FindAsync(walletId, paymentHssh, inbound, paymentId);
        if (payment != null)
        {
            if (failure && payment.Status == LightningPaymentStatus.Complete)
            {
                // ignore as per ldk docs that this might happen
            }
            else
            {
                payment.Status =failure ? LightningPaymentStatus.Failed : LightningPaymentStatus.Complete;
                payment.Preimage??= preimage;
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}