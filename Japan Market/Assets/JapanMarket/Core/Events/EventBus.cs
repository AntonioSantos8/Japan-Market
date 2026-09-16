using System;
using System.Collections.Generic;

namespace JapanMarket.Core
{
    /// <summary>
    /// Implementação do <see cref="IEventBus"/>.
    ///
    /// Quatro decisões que valem explicar:
    ///
    /// 1. Cada tipo de evento tem seu próprio canal, com um snapshot de handlers.
    ///    O snapshot só é reconstruído quando a lista muda, então publicar em
    ///    regime normal não aloca nada.
    ///
    /// 2. Cancelar uma inscrição durante um despacho não remove do snapshot em
    ///    uso — marca a inscrição como inativa. O despacho corrente pula handlers
    ///    inativos, então um objeto destruído no meio do evento nunca é chamado.
    ///
    /// 3. Um handler que lança não impede os outros de rodar: um sistema
    ///    quebrado não derruba a venda inteira. A exceção vai para o console.
    ///
    /// 4. A exceção do guard de recursão é a única que NÃO é engolida: ela é
    ///    relançada para chegar a quem publicou. Sem isso, o guard viraria
    ///    apenas mais uma linha no console e a recursão continuaria escondida.
    /// </summary>
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
                        // Erro de projeto — precisa chegar ao chamador, não ao console.
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

        /// <summary>Descarta todas as inscrições. Chamado por quem criou o bus.</summary>
        public void Clear()
        {
            foreach (Channel channel in _channels.Values)
            {
                foreach (Subscription s in channel.Subscriptions) s.Deactivate();
                channel.Clear();
            }
            _channels.Clear();
        }

        // ── internos ─────────────────────────────────────────────────────────

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

                // Sem isto, o array continuaria segurando inscrições descartadas
                // — e, através delas, os MonoBehaviours destruídos que capturaram.
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

                // Remover só marca o canal como sujo. Se estivermos no meio de um
                // despacho, o snapshot em uso continua válido e o guard IsActive
                // impede que este handler seja chamado.
                _channel?.Remove(this);
                _channel = null;
            }
        }
    }
}
