using System;
using JapanMarket.Core;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    /// <summary>
    /// Os casos aqui são exatamente as armadilhas que o código atual cai:
    /// listener que continua sendo chamado depois do objeto morrer, e recursão
    /// silenciosa entre sistemas que se disparam mutuamente.
    /// </summary>
    public sealed class EventBusTests
    {
        private readonly struct Ping : IGameEvent
        {
            public readonly int Value;
            public Ping(int value) => Value = value;
        }

        private readonly struct Pong : IGameEvent
        {
            public readonly int Value;
            public Pong(int value) => Value = value;
        }

        [Test]
        public void Handler_recebe_o_evento_publicado()
        {
            var bus = new EventBus();
            int received = 0;

            using (bus.Subscribe<Ping>(e => received = e.Value))
                bus.Publish(new Ping(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Publicar_sem_inscritos_nao_lanca()
        {
            var bus = new EventBus();
            Assert.DoesNotThrow(() => bus.Publish(new Ping(1)));
        }

        [Test]
        public void Dispose_cancela_a_inscricao()
        {
            var bus = new EventBus();
            int calls = 0;

            IDisposable sub = bus.Subscribe<Ping>(_ => calls++);
            bus.Publish(new Ping(1));
            sub.Dispose();
            bus.Publish(new Ping(2));

            Assert.AreEqual(1, calls, "Handler descartado não pode mais ser chamado.");
            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
        }

        [Test]
        public void Cancelar_durante_o_despacho_nao_chama_o_handler_cancelado()
        {
            // Este é o bug que mata NPC: um objeto se destrói dentro de um evento
            // e o listener seguinte da mesma lista ainda aponta para ele.
            var bus = new EventBus();
            int secondCalls = 0;
            IDisposable second = null;

            IDisposable first = bus.Subscribe<Ping>(_ => second.Dispose());
            second = bus.Subscribe<Ping>(_ => secondCalls++);

            bus.Publish(new Ping(1));

            Assert.AreEqual(0, secondCalls,
                "O segundo handler foi cancelado pelo primeiro e não podia rodar.");
            first.Dispose();
        }

        [Test]
        public void Inscrever_durante_o_despacho_nao_quebra_a_iteracao()
        {
            var bus = new EventBus();
            int lateCalls = 0;

            using IDisposable outer = bus.Subscribe<Ping>(_ =>
                bus.Subscribe<Ping>(__ => lateCalls++));

            Assert.DoesNotThrow(() => bus.Publish(new Ping(1)));
            Assert.AreEqual(0, lateCalls, "Quem entrou durante o despacho só ouve o próximo.");
        }

        [Test]
        public void Recursao_do_mesmo_tipo_lanca_em_vez_de_estourar_a_pilha()
        {
            var bus = new EventBus();
            using IDisposable sub = bus.Subscribe<Ping>(e => bus.Publish(new Ping(e.Value + 1)));

            // Tem que CHEGAR ao chamador. Se o bus engolisse esta exceção junto
            // com as dos handlers comuns, o guard viraria só uma linha no console
            // e a recursão continuaria invisível.
            var ex = Assert.Throws<EventBusRecursionException>(() => bus.Publish(new Ping(0)));
            Assert.AreEqual(typeof(Ping), ex.EventType);
        }

        [Test]
        public void Publicar_outro_tipo_de_dentro_de_um_handler_e_permitido()
        {
            // É assim que uma venda gera progresso de objetivo.
            var bus = new EventBus();
            int pongs = 0;

            using IDisposable a = bus.Subscribe<Ping>(_ => bus.Publish(new Pong(1)));
            using IDisposable b = bus.Subscribe<Pong>(_ => pongs++);

            Assert.DoesNotThrow(() => bus.Publish(new Ping(0)));
            Assert.AreEqual(1, pongs);
        }

        [Test]
        public void Handler_que_lanca_nao_impede_os_outros()
        {
            var bus = new EventBus();
            int reached = 0;

            using IDisposable bad  = bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
            using IDisposable good = bus.Subscribe<Ping>(_ => reached++);

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { bus.Publish(new Ping(1)); }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }

            Assert.AreEqual(1, reached, "Um sistema quebrado não pode derrubar os outros.");
        }
    }
}
