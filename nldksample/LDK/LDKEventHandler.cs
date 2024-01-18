﻿using org.ldk.structs;

namespace nldksample.LDK;

public class LDKEventHandler : EventHandlerInterface
{
    private readonly IEnumerable<ILDKEventHandler> _eventHandlers;
    private readonly LDKWalletLogger _ldkWalletLogger;

    public LDKEventHandler(IEnumerable<ILDKEventHandler> eventHandlers, LDKWalletLogger ldkWalletLogger)
    {
        _eventHandlers = eventHandlers;
        _ldkWalletLogger = ldkWalletLogger;
    }

    public void handle_event(Event _event)
    {
        _ldkWalletLogger.LogInformation($"Received event {_event.GetType()}");
        _eventHandlers.AsParallel().ForAll(handler => handler.Handle(_event).GetAwaiter().GetResult());
    }
}