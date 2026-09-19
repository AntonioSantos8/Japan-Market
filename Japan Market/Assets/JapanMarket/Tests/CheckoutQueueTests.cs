using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{

    public sealed class CheckoutQueueTests
    {
        [Test]
        public void Entrar_devolve_a_posicao_e_nao_duplica()
        {
            var queue = new CheckoutQueue();
            var ana = new FakeCustomer("ana");

            Assert.AreEqual(0, queue.Enqueue(ana));
            Assert.AreEqual(0, queue.Enqueue(ana), "Entering twice returns the same place.");
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Fila_cresce_sem_limite_de_pontos()
        {

            var queue = new CheckoutQueue();

            for (int i = 0; i < 50; i++)
                Assert.AreEqual(i, queue.Enqueue(new FakeCustomer()));

            Assert.AreEqual(50, queue.Count);
        }

        [Test]
        public void Sair_do_meio_so_avisa_quem_andou()
        {
            var queue = new CheckoutQueue();
            var a = new FakeCustomer("a");
            var b = new FakeCustomer("b");
            var c = new FakeCustomer("c");

            queue.Enqueue(a);
            queue.Enqueue(b);
            queue.Enqueue(c);

            var notified = new System.Collections.Generic.List<ICustomer>();
            queue.IndexChanged += (customer, _) => notified.Add(customer);

            queue.Remove(b);

            CollectionAssert.DoesNotContain(notified, a);
            CollectionAssert.Contains(notified, c);
            Assert.AreEqual(1, queue.IndexOf(c));
        }

        [Test]
        public void Sair_do_fim_nao_avisa_ninguem()
        {
            var queue = new CheckoutQueue();
            var a = new FakeCustomer("a");
            var b = new FakeCustomer("b");
            queue.Enqueue(a);
            queue.Enqueue(b);

            int notifications = 0;
            queue.IndexChanged += (_, __) => notifications++;

            queue.Remove(b);

            Assert.AreEqual(0, notifications);
        }

        [Test]
        public void Remover_quem_nao_esta_na_fila_e_inofensivo()
        {
            var queue = new CheckoutQueue();
            queue.Enqueue(new FakeCustomer("a"));

            Assert.IsFalse(queue.Remove(new FakeCustomer("stranger")));
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Dispersar_avisa_todo_mundo_e_esvazia()
        {

            var queue = new CheckoutQueue();
            var a = new FakeCustomer("a");
            var b = new FakeCustomer("b");
            queue.Enqueue(a);
            queue.Enqueue(b);

            queue.DisbandAll();

            Assert.IsTrue(a.Received(CustomerSignal.CheckoutLost));
            Assert.IsTrue(b.Received(CustomerSignal.CheckoutLost));
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void Dispersar_sobrevive_a_cliente_que_reage_saindo_da_fila()
        {

            var queue = new CheckoutQueue();
            var reentrant = new ReentrantCustomer(queue);
            queue.Enqueue(reentrant);
            queue.Enqueue(new FakeCustomer("b"));

            Assert.DoesNotThrow(() => queue.DisbandAll());
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void PruneDead_tira_quem_morreu_e_reindexa()
        {
            var queue = new CheckoutQueue();
            var morto = new FakeCustomer("dead");
            var vivo = new FakeCustomer("alive");
            queue.Enqueue(morto);
            queue.Enqueue(vivo);

            morto.IsAlive = false;
            queue.PruneDead();

            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(0, queue.IndexOf(vivo));
            Assert.IsTrue(queue.IsFront(vivo));
        }

        [Test]
        public void PruneDead_sem_nada_para_podar_nao_avisa_ninguem()
        {
            var queue = new CheckoutQueue();
            queue.Enqueue(new FakeCustomer("a"));
            queue.Enqueue(new FakeCustomer("b"));

            int notifications = 0;
            queue.IndexChanged += (_, __) => notifications++;

            queue.PruneDead();

            Assert.AreEqual(0, notifications,
                "Per-frame sweep cannot generate traffic when nothing changed.");
        }

        [Test]
        public void Front_de_fila_vazia_e_null_e_IsFront_de_null_e_falso()
        {
            var queue = new CheckoutQueue();

            Assert.IsNull(queue.Front);
            Assert.IsFalse(queue.IsFront(null));
            Assert.AreEqual(-1, queue.IndexOf(null));
        }

        private sealed class ReentrantCustomer : ICustomer
        {
            private readonly CheckoutQueue _queue;

            public ReentrantCustomer(CheckoutQueue queue) => _queue = queue;

            public int Id => 999;
            public UnityEngine.Vector3 Position => UnityEngine.Vector3.zero;
            public bool IsAlive => true;

            public void Notify(CustomerSignal signal) => _queue.Remove(this);
        }
    }
}
