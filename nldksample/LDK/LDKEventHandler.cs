using org.ldk.structs;

namespace nldksample.LDK;

public class LDKEventHandler : EventHandlerInterface
{
    private readonly IEnumerable<ILDKEventHandler> _eventHandlers;

    public LDKEventHandler(IEnumerable<ILDKEventHandler> eventHandlers)
    {
        _eventHandlers = eventHandlers;
    }

    public void handle_event(Event _event)
    {
        _eventHandlers.AsParallel().ForAll(handler => handler.Handle(_event).GetAwaiter().GetResult());
    }
}