using System;

namespace JapanMarket.Core
{

    public interface IEventBus
    {

        void Publish<T>(in T gameEvent) where T : struct, IGameEvent;

        IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent;

        int SubscriberCount<T>() where T : struct, IGameEvent;
    }
}
