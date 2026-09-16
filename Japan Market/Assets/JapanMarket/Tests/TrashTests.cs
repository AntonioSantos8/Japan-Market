using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    /// <summary>
    /// A separação de lixo sem cena: o saco, a doca e o caminhão.
    ///
    /// Nenhum destes testes abre a Unity, instancia prefab ou depende de
    /// colisão. É o que a regra "a lixeira não conhece o livro-razão" compra:
    /// o balanceamento da reciclagem inteira é verificável em milissegundos.
    /// </summary>
    [TestFixture]
    public class TrashTests
    {
        private readonly List<Object> _assets = new();

        private TrashCategory _plastico;
        private TrashCategory _metal;

        [SetUp]
        public void SetUp()
        {
            _plastico = Category("plastico", bonusYen: 100);
            _metal = Category("metal");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _assets.Count; i++)
                if (_assets[i] != null) Object.DestroyImmediate(_assets[i]);

            _assets.Clear();
        }

        private TrashCategory Category(string key, long bonusYen = 0)
        {
            var category = ScriptableObject.CreateInstance<TrashCategory>();
            category.name = key;
            category.EditorInitialize(key, Money.FromYen(bonusYen));

            _assets.Add(category);
            return category;
        }

        private TrashDefinition Trash(TrashCategory category, long valueYen)
        {
            var trash = ScriptableObject.CreateInstance<TrashDefinition>();
            trash.name = $"{category.Key}-{valueYen}";
            trash.EditorInitialize(category, Money.FromYen(valueYen));

            _assets.Add(trash);
            return trash;
        }

        // ── o saco ───────────────────────────────────────────────────────────

        [Test]
        public void O_primeiro_lixo_trava_a_categoria_do_saco()
        {
            // É a regra inteira do sistema: o saco não tem categoria escolhida,
            // ele herda a do primeiro item e trava. Sem a trava, "separar lixo"
            // vira "juntar lixo" e não existe decisão nenhuma a tomar.
            var bag = new TrashBag(capacity: 5);

            Assert.IsNull(bag.Category, "Saco vazio não tem categoria.");

            Assert.AreEqual(TrashSortResult.Ok, bag.TryAdd(Trash(_plastico, 10)));
            Assert.AreSame(_plastico, bag.Category);

            Assert.AreEqual(TrashSortResult.WrongCategory, bag.TryAdd(Trash(_metal, 50)));
            Assert.AreEqual(1, bag.Count, "O recusado não entra.");
        }

        [Test]
        public void O_saco_cheio_recusa_ate_da_propria_categoria()
        {
            var bag = new TrashBag(capacity: 2);

            bag.TryAdd(Trash(_plastico, 10));
            bag.TryAdd(Trash(_plastico, 10));

            Assert.IsTrue(bag.IsFull);
            Assert.AreEqual(TrashSortResult.BagFull, bag.TryAdd(Trash(_plastico, 10)));
            Assert.AreEqual(2, bag.Count);
        }

        [Test]
        public void Lixo_sem_categoria_nao_e_reciclavel()
        {
            var semCategoria = ScriptableObject.CreateInstance<TrashDefinition>();
            _assets.Add(semCategoria);

            var bag = new TrashBag();

            Assert.AreEqual(TrashSortResult.NotRecyclable, bag.TryAdd(semCategoria));
            Assert.AreEqual(TrashSortResult.NotRecyclable, bag.TryAdd(null));
            Assert.IsTrue(bag.IsEmpty);
            Assert.IsNull(bag.Category, "E não trava o saco numa categoria nula.");
        }

        [Test]
        public void O_bonus_de_saco_cheio_so_vale_cheio()
        {
            // O bônus é o que faz separar valer a pena: sem ele, entregar três
            // sacos pela metade rende igual a entregar um cheio.
            var bag = new TrashBag(capacity: 3);

            bag.TryAdd(Trash(_plastico, 10));
            bag.TryAdd(Trash(_plastico, 10));

            Assert.AreEqual(Money.FromYen(20), bag.Value, "Sem bônus com o saco pela metade.");

            bag.TryAdd(Trash(_plastico, 10));

            Assert.AreEqual(Money.FromYen(130), bag.Value, "30 de lixo + 100 de bônus.");
        }

        [Test]
        public void Esvaziar_o_saco_solta_a_categoria()
        {
            var bag = new TrashBag(capacity: 3);
            bag.TryAdd(Trash(_plastico, 10));

            bag.Clear();

            Assert.IsNull(bag.Category, "Senão a lixeira recusaria metal para sempre.");
            Assert.AreEqual(TrashSortResult.Ok, bag.TryAdd(Trash(_metal, 10)));
        }

        [Test]
        public void Restaurar_um_saco_que_ficou_vazio_nao_deixa_a_categoria_travada()
        {
            // O save tinha plástico, e o asset do plástico foi apagado do
            // projeto. O saco volta vazio — e não pode voltar travado em
            // plástico, ou a lixeira recusaria tudo sem nada explicando.
            var bag = new TrashBag(capacity: 5);
            bag.Restore(_plastico, new TrashDefinition[] { null, null });

            Assert.IsTrue(bag.IsEmpty);
            Assert.IsNull(bag.Category);
            Assert.AreEqual(TrashSortResult.Ok, bag.TryAdd(Trash(_metal, 10)));
        }

        [Test]
        public void Restaurar_respeita_a_capacidade_atual_e_a_categoria()
        {
            // O save é de uma versão em que o saco cabia 10; o balanceamento
            // mudou para 3. Vale a regra NOVA — restaurar 10 deixaria o saco num
            // estado que o jogo não consegue criar nem esvaziar direito.
            var bag = new TrashBag(capacity: 3);

            bag.Restore(_plastico, new[]
            {
                Trash(_plastico, 10), Trash(_plastico, 10),
                Trash(_metal, 99),                     // categoria errada: descartado
                Trash(_plastico, 10), Trash(_plastico, 10),
            });

            Assert.AreEqual(3, bag.Count);
            Assert.AreSame(_plastico, bag.Category);
        }

        // ── a doca e o caminhão ──────────────────────────────────────────────

        private TrashBag FullBag(TrashCategory category, int count, long eachYen)
        {
            var bag = new TrashBag(capacity: count);
            for (int i = 0; i < count; i++) bag.TryAdd(Trash(category, eachYen));

            return bag;
        }

        [Test]
        public void A_doca_soma_os_sacos_e_o_caminhao_leva_tudo()
        {
            var events = new EventBus();
            var service = new TrashService(events);

            Assert.IsTrue(service.TryDeposit(FullBag(_plastico, 2, 10)));   // 20 + 100
            Assert.IsTrue(service.TryDeposit(FullBag(_metal, 3, 50)));      // 150

            Assert.AreEqual(2, service.PendingBags);
            Assert.AreEqual(Money.FromYen(270), service.PendingValue);

            TrashCollected captured = default;
            using (events.Subscribe<TrashCollected>(e => captured = e))
            {
                Assert.AreEqual(Money.FromYen(270), service.Collect());
            }

            Assert.AreEqual(2, captured.BagCount);
            Assert.AreEqual(Money.FromYen(270), captured.Total);
            Assert.AreEqual(0, service.PendingBags, "A doca esvazia.");
        }

        [Test]
        public void O_mesmo_saco_nao_e_pago_duas_vezes()
        {
            // Dois gatilhos de doca sobrepostos disparam no mesmo frame para o
            // objeto que o jogador soltou.
            var service = new TrashService();
            TrashBag bag = FullBag(_plastico, 2, 10);

            Assert.IsTrue(service.TryDeposit(bag));
            Assert.IsFalse(service.TryDeposit(bag));

            Assert.AreEqual(1, service.PendingBags);
        }

        [Test]
        public void Saco_vazio_nao_entra_na_doca()
        {
            var service = new TrashService();

            Assert.IsFalse(service.TryDeposit(new TrashBag()));
            Assert.IsFalse(service.TryDeposit(null));
            Assert.AreEqual(0, service.PendingBags);
        }

        [Test]
        public void Um_caminhao_sem_nada_para_levar_nao_movimenta_o_caixa()
        {
            var service = new TrashService();

            int collected = 0;
            service.Collected += (_, __) => collected++;

            Assert.AreEqual(Money.Zero, service.Collect());
            Assert.AreEqual(0, collected, "Sem saco, sem evento — nada a mostrar na tela.");
        }

        [Test]
        public void Depositar_de_dentro_do_aviso_de_coleta_vai_para_o_dia_seguinte()
        {
            // A doca esvazia ANTES de avisar. Um assinante que deposite ali
            // estaria depositando para amanhã, e não pode encontrar a lista
            // antiga ainda cheia — seria pagar o saco novo hoje.
            var service = new TrashService();
            service.TryDeposit(FullBag(_plastico, 2, 10));

            TrashBag novo = FullBag(_metal, 1, 70);
            service.Collected += (_, __) => service.TryDeposit(novo);

            service.Collect();

            Assert.AreEqual(1, service.PendingBags);
            Assert.AreEqual(Money.FromYen(70), service.PendingValue);
        }

        // ── o caminhão dentro do fechamento do dia ───────────────────────────

        [Test]
        public void A_reciclagem_entra_no_relatorio_do_dia_em_que_foi_entregue()
        {
            // Esta é a razão de o IDailyIncomeSource existir. Assinando DayEnded,
            // o dinheiro do caminhão cairia no relatório do dia SEGUINTE, e o
            // jogador veria um dia que rendeu menos do que rendeu.
            var events = new EventBus();
            var clock = new GameClock(events, new GameClockSettings
            {
                DayStartHour = 6f, ClosingHour = 22f, EndOfDayHour = 24f,
                GameHoursPerRealSecond = 1f,
            });

            var ledger = new Ledger(events, clock, Money.FromYen(1000));
            var expenses = new ExpenseService(ledger);
            var reports = new DailyReportService(events, ledger, clock.Day);
            var cycle = new DayCycle(clock, expenses, reports, events, ledger);

            var trash = new TrashService(events);
            cycle.RegisterIncome(trash);

            trash.TryDeposit(FullBag(_plastico, 2, 10));   // 20 + 100 de bônus

            clock.RequestEndOfDay();

            Assert.AreEqual(Money.FromYen(1120), ledger.Balance);
            Assert.AreEqual(1, reports.ClosedReports.Count);
            Assert.AreEqual(0, trash.PendingBags);

            cycle.Dispose();
            reports.Dispose();
            ledger.Dispose();
        }

        [Test]
        public void Registrar_a_mesma_fonte_duas_vezes_nao_paga_duas_vezes()
        {
            var events = new EventBus();
            var clock = new GameClock(events, GameClockSettings.Default);
            var ledger = new Ledger(events, clock, Money.FromYen(1000));
            var expenses = new ExpenseService(ledger);
            var reports = new DailyReportService(events, ledger, clock.Day);
            var cycle = new DayCycle(clock, expenses, reports, events, ledger);

            var trash = new TrashService(events);
            cycle.RegisterIncome(trash);
            cycle.RegisterIncome(trash);

            trash.TryDeposit(FullBag(_metal, 1, 300));

            clock.RequestEndOfDay();

            Assert.AreEqual(Money.FromYen(1300), ledger.Balance);

            cycle.Dispose();
            reports.Dispose();
            ledger.Dispose();
        }
    }
}
