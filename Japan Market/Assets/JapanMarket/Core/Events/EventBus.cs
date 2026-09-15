using System;
using System.Collections.Generic;

namespace JapanMarket.Core
{

    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, Channel> _channels = new();

        public void Publish<T>(in T gameEvent) where T : struct, IGameEvent
        {
            if (!_channels.TryGetValue(typeof(T), out Channel channel)) return;

            if (channel.DispatchDepth > 0) throw new EventBusRecursionException(typeof(T));

            Subscription[] snapshot = channel.GetSnapshot();
            int count = channel.SnapshotCount;

            channel.DispatchDepth++;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Subscription sub = snapshot[i];
                    if (sub == null || !sub.IsActive) continue;

                    try
                    {
                        ((Action<T>)sub.Handler).Invoke(gameEvent);
                    }
                    catch (EventBusRecursionException)
                    {

                        throw;
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogException(e);
                    }
                }
            }
            finally
            {
                channel.DispatchDepth--;
            }
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Type key = typeof(T);
            if (!_channels.TryGetValue(key, out Channel channel))
            {
                channel = new Channel();
                _channels.Add(key, channel);
            }

            var subscription = new Subscription(channel, handler);
            channel.Add(subscription);
            return subscription;
        }

        public int SubscriberCount<T>() where T : struct, IGameEvent
        {
            if (!_channels.TryGetValue(typeof(T), out Channel channel)) return 0;

            int active = 0;
            foreach (Subscription s in channel.Subscriptions)
                if (s.IsActive) active++;
            return active;
        }

        public void Clear()
        {
            foreach (Channel channel in _channels.Values)
            {
                foreach (Subscription s in channel.Subscriptions) s.Deactivate();
                channel.Clear();
            }
            _channels.Clear();
        }

        private sealed class Channel
        {
            public readonly List<Subscription> Subscriptions = new();
            public int DispatchDepth;

            private Subscription[] _snapshot = Array.Empty<Subscription>();
            private bool _dirty = true;

            public int SnapshotCount { get; private set; }

            public void Add(Subscription s) { Subscriptions.Add(s); _dirty = true; }

            public void Remove(Subscription s)
            {
                if (Subscriptions.Remove(s)) _dirty = true;
            }

            public void Clear() { Subscriptions.Clear(); _dirty = true; }

            public Subscription[] GetSnapshot()
            {
                if (!_dirty) return _snapshot;

                if (_snapshot.Length < Subscriptions.Count)
                    _snapshot = new Subscription[Math.Max(4, Subscriptions.Count * 2)];

                Subscriptions.CopyTo(_snapshot, 0);
                SnapshotCount = Subscriptions.Count;

                if (_snapshot.Length > SnapshotCount)
                    Array.Clear(_snapshot, SnapshotCount, _snapshot.Length - SnapshotCount);

                _dirty = false;
                return _snapshot;
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Channel _channel;

            public readonly Delegate Handler;
            public bool IsActive { get; private set; } = true;

            public Subscription(Channel channel, Delegate handler)
            {
                _channel = channel;
                Handler = handler;
            }

            public void Deactivate() => IsActive = false;

            public void Dispose()
            {
                if (!IsActive) return;
                IsActive = false;

                _channel?.Remove(this);
                _channel = null;
            }
        }
    }
}
