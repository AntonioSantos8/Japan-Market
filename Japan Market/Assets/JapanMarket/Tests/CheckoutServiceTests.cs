using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    /// <summary>
    /// O serviço é onde mora a política de escolha de caixa e o fecho da venda.
    /// Como ele é C# puro, os casos extremos da sua lista — caixa removida com
    /// fila, caixa lotada, caixa inalcançável, loja sem caixa — viram testes de
    /// unidade em vez de tentativa e erro no Play Mode.
    /// </summary>
    public sealed class CheckoutServiceTests
    {
        private static Money Y(long yen) => Money.FromYen(yen);

        private FurnitureRegistry _registry;
        private EventBus _events;
        private CheckoutService _service;

        [SetUp]
        public void SetUp()
        {
            _registry = new FurnitureRegistry();
            _events = new EventBus();
            _service = new CheckoutService(_registry, _events);
        }

        private FakeCheckoutStation AddStation(Vector3 anchor)
        {
            var station = new FakeCheckoutStation { QueueAnchor = anchor };
            _registry.Register(new FakeFurniture().With<ICheckoutStation>(station));
            return station;
        }

        private static List<SaleLine> Lines(params long[] prices)
        {
            var lines = new List<SaleLine>(prices.Length);
            foreach (long price in prices) lines.Add(new SaleLine(null, Y(price)));
            return lines;
        }

        /// <summary>Leva um cliente da fila até a venda aberta, como o AtCounterState faz.</summary>
        private static CheckoutSession Serve(FakeCheckoutStation station, ICustomer customer,
                                             PaymentMethod method, params long[] prices)
        {
            Assert.IsTrue(station.TryJoinQueue(customer, out _));
            Assert.IsTrue(station.TryOpenSession(customer, Lines(prices), method,
                                                 out CheckoutSession session));
            return session;
        }

        // ── escolha da estação ───────────────────────────────────────────────

        [Test]
        public void Loja_sem_caixa_devolve_falso()
        {
            Assert.IsFalse(_service.TryFindBestStation(Vector3.zero, null, out _));
        }

        [Test]
        public void Escolhe_a_fila_mais_curta()
        {
            FakeCheckoutStation cheia = AddStation(new Vector3(0f, 0f, 0f));
            FakeCheckoutStation vazia = AddStation(new Vector3(50f, 0f, 0f));

            for (int i = 0; i < 3; i++) cheia.TryJoinQueue(new FakeCustomer(), out _);

            Assert.IsTrue(_service.TryFindBestStation(Vector3.zero, null,
                                                      out ICheckoutStation chosen));
            Assert.AreSame(vazia, chosen, "Fila curta ganha mesmo estando mais longe.");
        }

        [Test]
        public void Empate_de_fila_e_desempatado_pela_distancia()
        {
            // Sem o desempate, TODO cliente escolhe a primeira do registro e a
            // segunda caixa do jogador nunca é usada.
            FakeCheckoutStation longe = AddStation(new Vector3(0f, 0f, 40f));
            FakeCheckoutStation perto = AddStation(new Vector3(0f, 0f, 2f));

            Assert.IsTrue(_service.TryFindBestStation(Vector3.zero, null,
                                                      out ICheckoutStation chosen));
            Assert.AreSame(perto, chosen);
            Assert.AreNotSame(longe, chosen);
        }

        [Test]
        public void Caixa_desligada_e_ignorada()
        {
            FakeCheckoutStation desligada = AddStation(Vector3.zero);
            desligada.Open = false;

            Assert.IsFalse(_service.TryFindBestStation(Vector3.zero, null, out _));
        }

        [Test]
        public void Caixa_com_fila_no_limite_e_ignorada_mas_continua_operante()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);
            station.MaxQueueLength = 2;
            station.TryJoinQueue(new FakeCustomer(), out _);
            station.TryJoinQueue(new FakeCustomer(), out _);

            Assert.IsFalse(_service.TryFindBestStation(Vector3.zero, null, out _),
                "Não pode ser escolhida: mandaria o cliente levar um 'não' e voltar em loop.");
            Assert.IsTrue(station.IsOperational,
                "Mas quem já está na fila continua sendo atendido.");
        }

        [Test]
        public void Movel_morto_e_ignorado_mesmo_antes_do_Unregister()
        {
            // A janela de um frame entre destruir o objeto e o registro reagir.
            var station = new FakeCheckoutStation();
            var furniture = new FakeFurniture().With<ICheckoutStation>(station);
            _registry.Register(furniture);

            furniture.IsAlive = false;

            Assert.IsFalse(_service.TryFindBestStation(Vector3.zero, null, out _));
        }

        [Test]
        public void Caixa_inalcancavel_e_descartada_pelo_filtro_de_navegacao()
        {
            FakeCheckoutStation ilhada = AddStation(new Vector3(0f, 0f, 100f));
            FakeCheckoutStation acessivel = AddStation(new Vector3(0f, 0f, 3f));

            bool CanReach(Vector3 point) => point.z < 50f;

            Assert.IsTrue(_service.TryFindBestStation(Vector3.zero, CanReach,
                                                      out ICheckoutStation chosen));
            Assert.AreSame(acessivel, chosen);
            Assert.AreNotSame(ilhada, chosen);
        }

        // ── fecho da venda ───────────────────────────────────────────────────

        [Test]
        public void Nao_fecha_venda_com_item_por_passar()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);
            var customer = new FakeCustomer();
            CheckoutSession session = Serve(station, customer, PaymentMethod.Card, 155, 200);

            session.TryScanNext(out _);

            Assert.IsFalse(_service.TryCompleteSale(station));
            Assert.IsFalse(session.IsComplete);
            Assert.IsFalse(customer.Received(CustomerSignal.SaleFinished));
        }

        [Test]
        public void Nao_fecha_venda_em_dinheiro_sem_o_cliente_ter_entregado_o_valor()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);
            CheckoutSession session = Serve(station, new FakeCustomer(), PaymentMethod.Cash, 730);

            while (session.TryScanNext(out _)) { }
            session.SetAmountTendered(Y(500));

            Assert.IsFalse(_service.TryCompleteSale(station));
        }

        [Test]
        public void Venda_concluida_libera_o_cliente_e_o_balcao()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);
            var customer = new FakeCustomer();
            CheckoutSession session = Serve(station, customer, PaymentMethod.Cash, 155, 200);

            while (session.TryScanNext(out _)) { }
            session.SetAmountTendered(Y(1000));

            Assert.IsTrue(_service.TryCompleteSale(station));

            Assert.IsTrue(session.IsComplete);
            Assert.IsTrue(customer.Received(CustomerSignal.SaleFinished));
            Assert.IsNull(station.CurrentSession, "O balcão tem que ficar livre.");
            Assert.AreEqual(0, station.QueueLength, "E o cliente tem que sair da fila.");
            Assert.AreEqual(CheckoutStationState.Idle, station.State);
        }

        [Test]
        public void Venda_concluida_publica_o_evento_com_os_totais()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);
            var customer = new FakeCustomer();
            CheckoutSession session = Serve(station, customer, PaymentMethod.Card, 155, 200, 90);

            SaleCompleted captured = default;
            int fired = 0;
            using (_events.Subscribe<SaleCompleted>(e => { captured = e; fired++; }))
            {
                while (session.TryScanNext(out _)) { }
                Assert.IsTrue(_service.TryCompleteSale(station));
            }

            Assert.AreEqual(1, fired);
            Assert.AreEqual(customer.Id, captured.CustomerId);
            Assert.AreEqual(Y(445), captured.Revenue);
            Assert.AreEqual(3, captured.ItemCount);
            Assert.AreEqual(PaymentMethod.Card, captured.Method);
        }

        [Test]
        public void Venda_concluida_avisa_com_a_sessao_inteira()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);
            CheckoutSession session = Serve(station, new FakeCustomer(), PaymentMethod.Card, 155);

            CheckoutSession received = null;
            _service.SaleCompleted += s => received = s;

            while (session.TryScanNext(out _)) { }
            _service.TryCompleteSale(station);

            Assert.AreSame(session, received);
        }

        [Test]
        public void Fechar_duas_vezes_nao_cobra_duas_vezes()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);
            CheckoutSession session = Serve(station, new FakeCustomer(), PaymentMethod.Card, 155);

            int fired = 0;
            using (_events.Subscribe<SaleCompleted>(_ => fired++))
            {
                while (session.TryScanNext(out _)) { }

                Assert.IsTrue(_service.TryCompleteSale(station));
                Assert.IsFalse(_service.TryCompleteSale(station));
            }

            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Fechar_sem_venda_aberta_e_inofensivo()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);

            Assert.IsFalse(_service.TryCompleteSale(station));
            Assert.IsFalse(_service.TryCompleteSale(null));
        }

        [Test]
        public void Caixa_removida_no_meio_da_venda_nao_vira_venda()
        {
            // O lado de SERVIÇO do caso "jogador arranca a registradora": nada
            // é contabilizado. O que acontece dentro do balcão — quem é avisado,
            // com o quê, quantas vezes — está em CheckoutDeskTests, sobre o
            // mesmo CheckoutDesk que o componente usa.
            FakeCheckoutStation station = AddStation(Vector3.zero);
            var atendido = new FakeCustomer("atendido");
            var esperando = new FakeCustomer("esperando");

            CheckoutSession session = Serve(station, atendido, PaymentMethod.Cash, 730);
            station.TryJoinQueue(esperando, out _);
            while (session.TryScanNext(out _)) { }

            int sales = 0;
            using (_events.Subscribe<SaleCompleted>(_ => sales++))
            {
                station.Shutdown();
            }

            Assert.AreEqual(0, sales, "Ninguém pagou.");
            Assert.IsTrue(atendido.Received(CustomerSignal.CheckoutLost));
            Assert.IsTrue(esperando.Received(CustomerSignal.CheckoutLost));
            Assert.IsFalse(atendido.Received(CustomerSignal.SaleFinished));
            Assert.AreEqual(0, station.QueueLength);
            Assert.AreEqual(CheckoutStationState.Unavailable, station.State);
        }

        [Test]
        public void Cliente_que_desiste_no_balcao_libera_a_estacao_sem_sinal_falso()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);
            var customer = new FakeCustomer();
            Serve(station, customer, PaymentMethod.Cash, 730);

            station.LeaveQueue(customer);

            Assert.IsNull(station.CurrentSession);
            Assert.AreEqual(0, station.QueueLength);
            Assert.IsFalse(customer.Received(CustomerSignal.CheckoutLost),
                "Quem desistiu já sabe — avisá-lo o faria agir como se o caixa tivesse sumido.");
            Assert.IsFalse(customer.Received(CustomerSignal.SaleFinished));
        }

        [Test]
        public void Segundo_da_fila_nao_consegue_abrir_venda_na_frente_do_primeiro()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);
            var primeiro = new FakeCustomer("primeiro");
            var segundo = new FakeCustomer("segundo");

            station.TryJoinQueue(primeiro, out _);
            station.TryJoinQueue(segundo, out _);

            Assert.IsFalse(station.TryOpenSession(segundo, Lines(100), PaymentMethod.Cash, out _));
            Assert.IsTrue(station.TryOpenSession(primeiro, Lines(100), PaymentMethod.Cash, out _));
        }

        [Test]
        public void Fechar_uma_sessao_antiga_nao_derruba_a_venda_seguinte()
        {
            FakeCheckoutStation station = AddStation(Vector3.zero);
            var a = new FakeCustomer("a");
            var b = new FakeCustomer("b");

            CheckoutSession antiga = Serve(station, a, PaymentMethod.Card, 100);
            while (antiga.TryScanNext(out _)) { }
            Assert.IsTrue(_service.TryCompleteSale(station));

            CheckoutSession atual = Serve(station, b, PaymentMethod.Card, 200);

            station.CloseSession(antiga, SessionCloseReason.Abandoned);

            Assert.AreSame(atual, station.CurrentSession);
            Assert.AreEqual(1, station.QueueLength);
        }
    }
}
