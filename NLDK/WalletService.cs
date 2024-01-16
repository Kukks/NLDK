using AsyncKeyedLock;
using BTCPayServer.Lightning;
using Microsoft.EntityFrameworkCore;
using NBitcoin;
using NBXplorer.Models;

namespace NLDK;

public class WalletService
{
    private readonly IDbContextFactory<WalletContext> _dbContextFactory;
    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    private readonly Network _network;

    public WalletService(
        IDbContextFactory<WalletContext> dbContextFactory,
        AsyncKeyedLocker<string> asyncKeyedLocker,
        Network network)
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
            .Include(wallet => wallet.Channels)
            .Include(wallet => wallet.LightningPayments);
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

    public async Task<string> Create(Mnemonic mnemonic, string? name, string derivationPath, string currentBlockHash,
        string[] aliases,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var id = mnemonic.DeriveExtKey().GetPublicKey().GetHDFingerPrint().ToString();
        var wallet = new Wallet
        {
            Id = id,
            Name = name ?? id,
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
        var txToRemove = context.Transactions.Include(transaction => transaction.TransactionScripts)
            .Where(tx => tx.Hash == txHash);
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


    public async Task OnTransactionSeen(Wallet wallet, TrackedSource trackedSource, TransactionInformation transaction,
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


        var ws = transaction.Outputs.Select(output => new WalletScript()
        {
            WalletId = wallet.Id,
            ScriptId = output.ScriptPubKey.ToHex(),
            DerivationPath = output.KeyPath is null
                ? null
                : wallet.DerivationPath.Replace("*", output.KeyPath?.ToString()),
        }).Concat(
            transaction.Inputs.Select(output => new WalletScript()
            {
                WalletId = wallet.Id,
                ScriptId = output.ScriptPubKey.ToHex(),
                DerivationPath = output.KeyPath is null
                    ? null
                    : wallet.DerivationPath.Replace("*", output.KeyPath?.ToString()),
            }));
        if (trackedSource is DerivationSchemeTrackedSource)
        {
            await context.WalletScripts.UpsertRange(ws).WhenMatched((a, b) => new WalletScript()
            {
                DerivationPath = a.DerivationPath ?? b.DerivationPath,
            }).RunAsync(cancellationToken);
        }
        else
        {
            await context.WalletScripts.UpsertRange(ws).NoUpdate().RunAsync(cancellationToken);
        }

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

    public async Task HandlePendingTxs(Func<List<Transaction>, Task> process,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var txs = await context.Transactions.Where(transaction => transaction.BlockHash == null)
            .ToListAsync(cancellationToken: cancellationToken);
        await process.Invoke(txs);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddOrUpdateChannel(Channel channel, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.Channels.Upsert(channel).RunAsync(cancellationToken);
    }

    public async Task AddOrUpdateArbitraryData(string? walletId, string key, byte[]? value,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (value is null)
        {
            try
            {
                context.ArbitraryData.Remove(new ArbitraryData()
                {
                    Key = walletId + key
                });
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException e)
            {
                return;
            }
        }

        await context.ArbitraryData.Upsert(new ArbitraryData()
        {
            WalletId = walletId,
            Key = walletId + key,
            Value = value
        }).RunAsync(cancellationToken);
    }

    public async Task TrackScript(string walletId, NBitcoin.Script script,
        CancellationToken cancellationToken = default)
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

    public async Task AddSpendableToCoin(string walletId, (OutPoint outpoint, TxOut txOut, byte[] write)[] set,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.Scripts.UpsertRange(set.Select(tuple => new Script()
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
                channel.WalletId == walletId && set.Any(tuple =>
                    tuple.outpoint.Hash.ToString() == channel.Coin.SpendingTransactionHash &&
                    tuple.outpoint.N == channel.Coin.SpendingTransactionInputIndex))
            .ForEachAsync(
                channel =>
                {
                    channel.SpendableData = set.First(tuple =>
                        tuple.outpoint.Hash.ToString() == channel.Coin.SpendingTransactionHash &&
                        tuple.outpoint.N == channel.Coin.SpendingTransactionInputIndex).write;
                }, cancellationToken: cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Payment(LightningPayment lightningPayment, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.LightningPayments.Upsert(lightningPayment).RunAsync(cancellationToken);
    }


    public async Task<(NBitcoin.Transaction Tx, ICoin[] SpentCoins, NBitcoin.Script Change)?> CreateTransaction(
        string walletId, List<TxOut> txOuts, FeeRate feeRate, List<NBitcoin.Coin>? explicitIns = null,
        CancellationToken cancellationToken = default)
    {
        var changeScript = (await DeriveScript(walletId, cancellationToken)).ToScript();
        var txBuilder = _network.CreateTransactionBuilder().SetChange(changeScript)
            .SendEstimatedFees(feeRate);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        //fetch all coins that are not spent associated with this wallet
        var coins = await context.Coins.Include(coin => coin.Script).ThenInclude(script1 => script1.WalletScripts)
            .Where(coin => coin.Script.WalletScripts.Any(walletScript =>
                walletScript.WalletId == walletId && walletScript.DerivationPath != null &&
                coin.SpendingTransactionHash == null)).ToListAsync(cancellationToken: cancellationToken);
        //group coins by walletscript
        var coinsByWalletScript = coins
            .GroupBy(coin => coin.Script.WalletScripts.First(walletScript =>
                walletScript.WalletId == walletId && walletScript.DerivationPath != null))
            .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());

        var wallet = await context.Wallets.FindAsync(walletId);

        var mnemonic = new Mnemonic(wallet!.Mnemonic).DeriveExtKey()!;
        NBitcoin.Transaction? tx;
        if (!txOuts.Any() && explicitIns?.Any() is true)
        {
            txBuilder.AddCoins(explicitIns.ToArray());

            txBuilder.SendAllRemainingToChange();
            while (coinsByWalletScript.Any())
            {
                try
                {
                    tx = txBuilder.BuildTransaction(true);
                    return (tx, txBuilder.FindSpentCoins(tx), changeScript);
                }
                catch (NotEnoughFundsException e)
                {
                    var scriptSet = coinsByWalletScript.First();
                    var newCoin = scriptSet.Value.First();
                    var key = mnemonic.Derive(KeyPath.Parse(scriptSet.Key.DerivationPath));
                    txBuilder.AddCoins(newCoin.AsCoin());
                    txBuilder.AddKeys(key);
                    scriptSet.Value.Remove(newCoin);
                    if (scriptSet.Value.Count == 0)
                        coinsByWalletScript.Remove(scriptSet.Key);
                }
            }

            return null;
        }

        txBuilder = coinsByWalletScript.Aggregate(txBuilder,
            (current, keysAndCoin) => current.AddKeys(mnemonic.Derive(KeyPath.Parse(keysAndCoin.Key.DerivationPath)))
                .AddCoins(keysAndCoin.Value.Select(coin => new NBitcoin.Coin(uint256.Parse(coin.FundingTransactionHash),
                    (uint) coin.FundingTransactionOutputIndex, Money.Coins(coin.Value),
                    NBitcoin.Script.FromHex(coin.ScriptId)))));
        txBuilder = txOuts.Aggregate(txBuilder, (current, c) => current.Send(c.ScriptPubKey, c.Value));

        tx = txBuilder.BuildTransaction(true);

        return (tx, txBuilder.FindSpentCoins(tx), changeScript);
    }

    public async Task PaymentUpdate(string walletId, string paymentHssh, bool inbound, string paymentId, bool failure,
        string? preimage, CancellationToken cancellationToken = default)
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
                payment.Status = failure ? LightningPaymentStatus.Failed : LightningPaymentStatus.Complete;
                payment.Preimage ??= preimage;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<NBitcoin.Transaction> SignTransaction(string walletId, NBitcoin.Transaction tx1,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var outpointsToMatch = tx1.Inputs.Select(input => input.PrevOut).ToArray();
        //fetch all coins that are not spent associated with this wallet
        var coins = await context.Coins.Include(coin => coin.Script)
            .ThenInclude(script1 => script1.WalletScripts)
            .Where(coin =>
                outpointsToMatch.Any(point =>
                    point.Hash.ToString() == coin.FundingTransactionHash &&
                    point.N == coin.FundingTransactionOutputIndex) &&
                coin.Script.WalletScripts.Any(
                    walletScript => walletScript.WalletId == walletId &&
                                    walletScript.DerivationPath != null &&
                                    coin.SpendingTransactionHash == null))
            .ToListAsync(cancellationToken: cancellationToken);
        //group coins by walletscript
        var coinsByWalletScript = coins
            .GroupBy(coin => coin.Script.WalletScripts.First(walletScript =>
                walletScript.WalletId == walletId && walletScript.DerivationPath != null))
            .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());

        var txBuilder = _network.CreateTransactionBuilder();
        var wallet = await context.Wallets.FindAsync(walletId);

        var mnemonic = new Mnemonic(wallet!.Mnemonic).DeriveExtKey()!;
        txBuilder = coinsByWalletScript.Aggregate(txBuilder,
            (current, keysAndCoin) => current.AddKeys(mnemonic.Derive(KeyPath.Parse(keysAndCoin.Key.DerivationPath)))
                .AddCoins(keysAndCoin.Value.Select(coin => new NBitcoin.Coin(uint256.Parse(coin.FundingTransactionHash),
                    (uint) coin.FundingTransactionOutputIndex, Money.Coins(coin.Value),
                    NBitcoin.Script.FromHex(coin.ScriptId)))));
        return txBuilder.SignTransactionInPlace(tx1);
    }

    public async Task<Dictionary<string, byte[]>> GetArbitraryData(string? walletId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ArbitraryData.Where(data => data.WalletId == walletId)
            .ToDictionaryAsync(data => data.Key, data => data.Value, cancellationToken);
    }
}