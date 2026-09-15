using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{

    public sealed class CheckoutDeskTests
    {
        private static Money Y(long yen) => Money.FromYen(yen);

        private static List<SaleLine> Lines(params long[] prices)
        {
            var lines = new List<SaleLine>(prices.Length);
            foreach (long price in prices) lines.Add(new SaleLine(null, Y(price)));
            return lines;
        }

        [Test]
        public void Nasce_ocioso_e_vira_esperando_com_fila()
        {
            var desk = new CheckoutDesk();
            Assert.AreEqual(CheckoutStationState.Idle, desk.State);

            desk.TryJoinQueue(new FakeCustomer(), out _);

            Assert.AreEqual(CheckoutStationState.Waiting, desk.State);
        }

        [Test]
        public void Venda_aberta_poe_em_atendimento_e_fechada_devolve_ao_ocioso()
        {
            var desk = new CheckoutDesk();
            var customer = new FakeCustomer();
            desk.TryJoinQueue(customer, out _);

            Assert.IsTrue(desk.TryOpenSession(customer, Lines(100), PaymentMethod.Card,
                                              out CheckoutSession session));
            Assert.AreEqual(CheckoutStationState.Serving, desk.State);

            desk.CloseSession(session, SessionCloseReason.Completed);

            Assert.AreEqual(CheckoutStationState.Idle, desk.State);
            Assert.AreEqual(0, desk.QueueLength);
        }

        [Test]
        public void StateChanged_nao_dispara_quando_o_estado_nao_muda()
        {
            var desk = new CheckoutDesk();
            desk.TryJoinQueue(new FakeCustomer(), out _);

            int fired = 0;
            desk.StateChanged += _ => fired++;

            desk.TryJoinQueue(new FakeCustomer(), out _);   
            desk.Refresh();

            Assert.AreEqual(0, fired);
        }

        [Test]
        public void Caixa_desligado_nao_aceita_ninguem()
        {
            bool open = false;
            var desk = new CheckoutDesk(() => open);

            Assert.IsFalse(desk.TryJoinQueue(new FakeCustomer(), out _));
            Assert.IsFalse(desk.AcceptsNewCustomers);

            open = true;
            Assert.IsTrue(desk.TryJoinQueue(new FakeCustomer(), out _));
        }

        [Test]
        public void Fila_no_limite_recusa_entrada_mas_continua_operante()
        {
            var desk = new CheckoutDesk { MaxQueueLength = 2 };
            desk.TryJoinQueue(new FakeCustomer(), out _);
            desk.TryJoinQueue(new FakeCustomer(), out _);

            Assert.IsFalse(desk.TryJoinQueue(new FakeCustomer(), out _));
            Assert.IsFalse(desk.AcceptsNewCustomers);
            Assert.IsTrue(desk.IsOperational, "Those already in the queue continue to be served.");
        }

        [Test]
        public void Limite_zero_significa_sem_limite()
        {
            var desk = new CheckoutDesk { MaxQueueLength = 0 };

            for (int i = 0; i < 40; i++)
                Assert.IsTrue(desk.TryJoinQueue(new FakeCustomer(), out _));

            Assert.AreEqual(40, desk.QueueLength);
        }

        [Test]
        public void Entrar_duas_vezes_devolve_o_mesmo_lugar()
        {
            var desk = new CheckoutDesk();
            var primeiro = new FakeCustomer();
            var segundo = new FakeCustomer();

            desk.TryJoinQueue(primeiro, out _);
            desk.TryJoinQueue(segundo, out int antes);

            Assert.IsTrue(desk.TryJoinQueue(segundo, out int depois));
            Assert.AreEqual(antes, depois);
            Assert.AreEqual(2, desk.QueueLength);
        }

        [Test]
        public void Quem_nao_e_o_primeiro_nao_abre_venda()
        {
            var desk = new CheckoutDesk();
            var primeiro = new FakeCustomer("first");
            var segundo = new FakeCustomer("second");
            desk.TryJoinQueue(primeiro, out _);
            desk.TryJoinQueue(segundo, out _);

            Assert.IsFalse(desk.TryOpenSession(segundo, Lines(100), PaymentMethod.Cash, out _));
            Assert.IsTrue(desk.TryOpenSession(primeiro, Lines(100), PaymentMethod.Cash, out _));
        }

        [Test]
        public void Nao_abre_duas_vendas_ao_mesmo_tempo()
        {
            var desk = new CheckoutDesk();
            var customer = new FakeCustomer();
            desk.TryJoinQueue(customer, out _);

            Assert.IsTrue(desk.TryOpenSession(customer, Lines(100), PaymentMethod.Card, out _));
            Assert.IsFalse(desk.TryOpenSession(customer, Lines(100), PaymentMethod.Card, out _));
        }

        [Test]
        public void Venda_sem_linha_nao_abre()
        {
            var desk = new CheckoutDesk();
            var customer = new FakeCustomer();
            desk.TryJoinQueue(customer, out _);

            Assert.IsFalse(desk.TryOpenSession(customer, Lines(), PaymentMethod.Cash, out _));
            Assert.IsFalse(desk.TryOpenSession(customer, null, PaymentMethod.Cash, out _));
        }

        [Test]
        public void Abandonar_nao_manda_sinal_nenhum()
        {

            var desk = new CheckoutDesk();
            var customer = new FakeCustomer();
            desk.TryJoinQueue(customer, out _);
            desk.TryOpenSession(customer, Lines(100), PaymentMethod.Cash, out CheckoutSession s);

            desk.CloseSession(s, SessionCloseReason.Abandoned);

            Assert.AreEqual(0, customer.Signals.Count);
            Assert.IsNull(desk.CurrentSession);
            Assert.AreEqual(0, desk.QueueLength);
        }

        [Test]
        public void Sair_da_fila_atendido_encerra_a_venda()
        {
            var desk = new CheckoutDesk();
            var customer = new FakeCustomer();
            desk.TryJoinQueue(customer, out _);
            desk.TryOpenSession(customer, Lines(100), PaymentMethod.Cash, out _);

            desk.LeaveQueue(customer);

            Assert.IsNull(desk.CurrentSession, "Otherwise the desk gets stuck forever.");
            Assert.AreEqual(CheckoutStationState.Idle, desk.State);
        }

        [Test]
        public void Fechar_sessao_antiga_nao_derruba_a_atual()
        {
            var desk = new CheckoutDesk();
            var a = new FakeCustomer("a");
            var b = new FakeCustomer("b");

            desk.TryJoinQueue(a, out _);
            desk.TryOpenSession(a, Lines(100), PaymentMethod.Card, out CheckoutSession antiga);
            desk.CloseSession(antiga, SessionCloseReason.Completed);

            desk.TryJoinQueue(b, out _);
            desk.TryOpenSession(b, Lines(200), PaymentMethod.Card, out CheckoutSession atual);

            desk.CloseSession(antiga, SessionCloseReason.StationLost);

            Assert.AreSame(atual, desk.CurrentSession);
            Assert.IsFalse(b.Received(CustomerSignal.CheckoutLost));
        }

        [Test]
        public void Caixa_arrancado_com_fila_e_venda_aberta_avisa_todo_mundo_uma_vez()
        {
            bool operational = true;
            var desk = new CheckoutDesk(() => operational);

            var atendido = new FakeCustomer("served");
            var esperando = new FakeCustomer("waiting");
            var ultimo = new FakeCustomer("last");

            desk.TryJoinQueue(atendido, out _);
            desk.TryJoinQueue(esperando, out _);
            desk.TryJoinQueue(ultimo, out _);
            desk.TryOpenSession(atendido, Lines(730), PaymentMethod.Cash, out _);

            operational = false;
            desk.Shutdown();

            Assert.AreEqual(1, atendido.CountOf(CustomerSignal.CheckoutLost),
                "Whoever was being served cannot receive the warning twice.");
            Assert.AreEqual(1, esperando.CountOf(CustomerSignal.CheckoutLost));
            Assert.AreEqual(1, ultimo.CountOf(CustomerSignal.CheckoutLost));

            Assert.IsFalse(atendido.Received(CustomerSignal.SaleFinished), "Nobody paid.");
            Assert.IsNull(desk.CurrentSession);
            Assert.AreEqual(0, desk.QueueLength);
            Assert.AreEqual(CheckoutStationState.Unavailable, desk.State);
        }

        [Test]
        public void Caixa_arrancado_vazio_nao_avisa_ninguem_e_nao_lanca()
        {
            bool operational = true;
            var desk = new CheckoutDesk(() => operational);

            operational = false;
            Assert.DoesNotThrow(() => desk.Shutdown());
            Assert.AreEqual(CheckoutStationState.Unavailable, desk.State);
        }

        [Test]
        public void Cliente_que_morreu_na_fila_e_removido_pela_varredura()
        {
            var desk = new CheckoutDesk();
            var morto = new FakeCustomer("dead");
            var vivo = new FakeCustomer("alive");
            desk.TryJoinQueue(morto, out _);
            desk.TryJoinQueue(vivo, out _);

            morto.IsAlive = false;

            Assert.IsTrue(desk.PruneDead());
            Assert.AreEqual(1, desk.QueueLength);
            Assert.IsTrue(desk.IsFront(vivo));
        }

        [Test]
        public void Varredura_fecha_a_venda_de_quem_morreu_atendido()
        {

            var desk = new CheckoutDesk();
            var customer = new FakeCustomer();
            desk.TryJoinQueue(customer, out _);
            desk.TryOpenSession(customer, Lines(100), PaymentMethod.Cash, out _);

            customer.IsAlive = false;
            Assert.IsTrue(desk.PruneDead());

            Assert.IsNull(desk.CurrentSession);
            Assert.AreEqual(CheckoutStationState.Idle, desk.State);
        }

        [Test]
        public void Varredura_sem_nada_para_podar_nao_faz_nada()
        {
            var desk = new CheckoutDesk();
            desk.TryJoinQueue(new FakeCustomer(), out _);

            int stateChanges = 0;
            desk.StateChanged += _ => stateChanges++;

            Assert.IsFalse(desk.PruneDead());
            Assert.AreEqual(0, stateChanges);
        }
    }
}
