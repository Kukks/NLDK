using org.ldk.enums;
using org.ldk.structs;

namespace nldksample.LDK;

public class LDKLogger : LoggerInterface
{
    private readonly ILogger<LDKLogger> _logger;

    public LDKLogger(ILogger<LDKLogger> logger)
    {
        _logger = logger;
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
}