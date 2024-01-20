using org.ldk.enums;
using org.ldk.structs;

namespace nldksample.LDK;

public class LDKWalletLoggerFactory : ILoggerFactory
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
    
    public List<string> Logs { get; } = new List<string>();

    public ILogger CreateLogger(string category)
    {
        var categoryName = (string.IsNullOrWhiteSpace(category) ? "LDK": $"LDK.{category}") +
                           $"[{_currentWalletService.CurrentWallet}]" ;
        LoggerWrapper logger = new LoggerWrapper(_inner.CreateLogger(categoryName));

        logger.LogEvent += (sender, message) => Logs.Add(DateTime.Now.ToShortTimeString() +" "+categoryName +message);
        
        return logger;
    }
}

public class LoggerWrapper:ILogger
{
    private readonly ILogger _inner;

    public LoggerWrapper(ILogger inner)
    {
        _inner = inner;
    }
    
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _inner.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _inner.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _inner.Log(logLevel, eventId, state, exception, formatter);
        LogEvent?.Invoke(this, formatter(state, exception));
    }
    
    public event EventHandler<string>? LogEvent;
}

public class LDKWalletLogger : LDKLogger
{
    public LDKWalletLogger(LDKWalletLoggerFactory ldkWalletLoggerFactory) : base(ldkWalletLoggerFactory)
    {
    }
}

public class LDKLogger : LoggerInterface, ILogger
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _baseLogger;

    public LDKLogger(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _baseLogger = loggerFactory.CreateLogger("");
    }

    public virtual void log(Record record)
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
        _loggerFactory.CreateLogger(record.get_module_path()).Log(level, "{Args}", record.get_args());
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _baseLogger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _baseLogger.IsEnabled(logLevel);
    }

    public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _baseLogger.Log(logLevel, eventId, state, exception, formatter);
    }
}
