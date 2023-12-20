using org.ldk.enums;
using org.ldk.structs;

namespace nldksample.LDK;


public class LDKWalletLoggerFactory: ILoggerFactory
{
    private readonly CurrentWalletService _currentWalletService;
    private readonly ILoggerFactory _inner;

    public LDKWalletLoggerFactory(CurrentWalletService currentWalletService, ILoggerFactory loggerFactory)
    {
        _currentWalletService = currentWalletService;
        _inner = loggerFactory;
    }
    public void Dispose()
    {
        //ignore as this is scoped
    }

    public void AddProvider(ILoggerProvider provider)
    {
        _inner.AddProvider(provider);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _inner.CreateLogger($"LDK[{_currentWalletService.CurrentWallet}]{categoryName}");
    }
}

public class LDKWalletLogger: LDKLogger
{
    public LDKWalletLogger(LDKWalletLoggerFactory ldkWalletLoggerFactory):base(ldkWalletLoggerFactory.CreateLogger(""))
    {
    }
}

public class LDKLogger : LoggerInterface, ILogger
{
    private readonly ILogger _logger;

    public LDKLogger(ILogger logger)
    {
        _logger = logger;
    }
    public LDKLogger(ILoggerFactory loggerFactory):this(loggerFactory.CreateLogger("LDK"))
    {
        
    }

    public void log(Record record)
    {
        var level = record.get_level() switch
        {
            Level.LDKLevel_Trace => LogLevel.Trace,
            Level.LDKLevel_Debug => LogLevel.Debug,
            Level.LDKLevel_Info => LogLevel.Information,
            Level.LDKLevel_Warn => LogLevel.Warning,
            Level.LDKLevel_Error => LogLevel.Error,
            Level.LDKLevel_Gossip => LogLevel.Trace,
        };
        _logger.Log(level, $"[{record.get_module_path()}] {record.get_args()}");
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    
    {
        return _logger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }
}