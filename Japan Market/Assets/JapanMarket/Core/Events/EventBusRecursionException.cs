using System;

namespace JapanMarket.Core
{

    public sealed class EventBusRecursionException : InvalidOperationException
    {
        public Type EventType { get; }

        public EventBusRecursionException(Type eventType)
            : base($"[EventBus] '{eventType.Name}' was published from within a handler " +
                   $"of '{eventType.Name}' itself. This is recursion and always indicates a " +
                   "design error. Publish a different event type, or defer the publication " +
                   "until after the current dispatch.")
        {
            EventType = eventType;
        }
    }
}
