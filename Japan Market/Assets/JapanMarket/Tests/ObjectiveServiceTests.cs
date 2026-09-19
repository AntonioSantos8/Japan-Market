using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    /// <summary>
    /// A lista de objetivos sem cena.
    ///
    /// O que estes testes provam, além do óbvio: que adicionar um objetivo é
    /// criar um asset. Não existe aqui nenhum "se for o objetivo tal" — cada
    /// caso monta a definição que quer e o serviço trata todas igual, porque é
    /// literalmente o mesmo caminho de código.
    /// </summary>
    [TestFixture]
    public class ObjectiveServiceTests
    {
        private EventBus _events;
        private GameClock _clock;
        private Ledger _ledger;
        private StoreLevelService _level;
        private FakeUnlockContext _context;

        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _events = new EventBus();
            _clock = new GameClock(_events, new GameClockSettings
            {
                DayStartHour = 6f, ClosingHour = 22f, EndOfDayHour = 24f,
                GameHoursPerRealSecond = 1f,
            });

            _ledger = new Ledger(_events, _clock, Money.FromYen(1000));
            _level = new StoreLevelService(_events);
            _context = new FakeUnlockContext();
        }

        [TearDown]
        public void TearDown()
        {
            _level.Dispose();
            _ledger.Dispose();

            for (int i = 0; i < _assets.Count; i++)
                if (_assets[i] != null) Object.DestroyImmediate(_assets[i]);

            _assets.Clear();
        }

        // ── montagem ─────────────────────────────────────────────────────────

        private T Condition<T>(int target) where T : ObjectiveCondition
        {
            var condition = ScriptableObject.CreateInstance<T>();
            condition.EditorSetTarget(target);

            _assets.Add(condition);
            return condition;
        }

        private ObjectiveDefinition Objective(ObjectiveCondition[] conditions,
                                              Money money = default, int xp = 0,
                                              string flag = null,
                                              UnlockCondition unlock = null,
                                              int sortOrder = 0,
                                              string name = "Objetivo")
        {
            var definition = ScriptableObject.CreateInstance<ObjectiveDefinition>();
            definition.name = name;
            definition.EditorInitialize(
                ObjectiveId.Generate(), conditions,
                new ObjectiveReward { Money = money, XP = xp, Flag = flag },
                unlock, sortOrder);

            _assets.Add(definition);
            return definition;
        }

        private FlagUnlock Gate(string flag)
        {
            var gate = ScriptableObject.CreateInstance<FlagUnlock>();
            gate.EditorSetFlag(flag);

            _assets.Add(gate);
            return gate;
        }

        private ObjectiveService Service(params ObjectiveDefinition[] objectives) =>
            new(new FakeObjectiveCatalog(objectives), _events,
                _ledger, _level, _context, _context);

        private void Sell(int items, long revenueYen = 0) =>
            _events.Publish(new SaleCompleted(1, default, Money.FromYen(revenueYen),
                                              Money.Zero, items, PaymentMethod.Cash));

        // ── progresso ────────────────────────────────────────────────────────

        [Test]
        public void O_objetivo_entra_em_jogo_e_conta_o_que_passa_no_barramento()
        {
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(5) });

            using ObjectiveService service = Service(definition);

            Assert.AreEqual(1, service.Active.Count);
            Assert.AreEqual(0, service.Active[0].ProgressOf(0));

            Sell(3);

            Assert.AreEqual(3, service.Active[0].ProgressOf(0));
            Assert.IsFalse(service.Active[0].IsCompleted);
        }

        [Test]
        public void Tutorial_suspende_progresso_e_recompensa_ate_ser_concluido()
        {
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(1) },
                money: Money.FromYen(500));

            using ObjectiveService service = Service(definition);
            service.SetTrackingEnabled(false);

            Sell(1);
            service.Flush();

            Assert.IsFalse(service.TrackingEnabled);
            Assert.AreEqual(0, service.Active[0].ProgressOf(0));
            Assert.IsFalse(service.Active[0].IsCompleted);
            Assert.AreEqual(Money.FromYen(1000), _ledger.Balance);

            service.SetTrackingEnabled(true);
            Sell(1);
            service.Flush();

            Assert.IsTrue(service.TrackingEnabled);
            Assert.IsTrue(service.Active[0].IsCompleted);
            Assert.AreEqual(Money.FromYen(1500), _ledger.Balance);
        }

        [Test]
        public void O_progresso_nunca_passa_do_alvo_na_tela()
        {
            // A barra do jogador não pode marcar 8 de 5. O bruto continua 8 —
            // é o que o save guarda, e é o que faz a conta bater se um dia o
            // alvo do asset mudar.
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(5) });

            using ObjectiveService service = Service(definition);
            Sell(8);

            Assert.AreEqual(5, service.Active[0].ProgressOf(0));
            Assert.AreEqual(8, service.Active[0].RawProgressOf(0));
            Assert.AreEqual(1f, service.Active[0].NormalizedProgress, 0.001f);
        }

        [Test]
        public void Cumprir_as_condicoes_nao_conclui_sozinho_antes_do_Flush()
        {
            // Não é detalhe de implementação: é o contrato que impede a
            // recompensa de ser paga de dentro do despacho de um evento.
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(2) },
                money: Money.FromYen(500));

            using ObjectiveService service = Service(definition);
            Sell(2);

            Assert.IsTrue(service.Active[0].AllConditionsMet);
            Assert.IsFalse(service.Active[0].IsCompleted);
            Assert.AreEqual(Money.FromYen(1000), _ledger.Balance, "Nada pago ainda.");

            service.Flush();

            Assert.IsTrue(service.Active[0].IsCompleted);
            Assert.AreEqual(Money.FromYen(1500), _ledger.Balance);
        }

        // ── recompensa ───────────────────────────────────────────────────────

        [Test]
        public void A_recompensa_paga_dinheiro_XP_e_flag_uma_vez_so()
        {
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(1) },
                money: Money.FromYen(500), xp: 40, flag: "primeira_venda");

            using ObjectiveService service = Service(definition);

            int completions = 0;
            service.ObjectiveCompleted += _ => completions++;

            Sell(1);
            int xpBeforeReward = _level.CurrentXP;

            service.Flush();
            Assert.AreEqual(xpBeforeReward + 40, _level.CurrentXP,
                "A recompensa soma 40 ao XP que a venda já concedeu.");
            int xpAfterReward = _level.CurrentXP;
            service.Flush();
            Assert.AreEqual(xpAfterReward, _level.CurrentXP, "Flush repetido não paga outra recompensa.");
            Sell(5);
            int xpAfterSecondSale = _level.CurrentXP;
            service.Flush();

            Assert.AreEqual(1, completions);
            Assert.AreEqual(Money.FromYen(1500), _ledger.Balance);
            Assert.AreEqual(xpAfterSecondSale, _level.CurrentXP,
                "Vendas posteriores continuam dando XP, sem repetir a recompensa do objetivo.");
            Assert.AreEqual(1, _context.RaiseCount);
            Assert.IsTrue(_context.HasFlag("primeira_venda"));
        }

        [Test]
        public void O_objetivo_concluido_para_de_ouvir_o_barramento()
        {
            // Um objetivo concluído que continua inscrito é trabalho por venda,
            // para sempre, sem poder mudar nada — e some do perfil como "o
            // EventBus está caro".
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(1) });

            using ObjectiveService service = Service(definition);

            Sell(1);
            service.Flush();

            Sell(50);

            Assert.AreEqual(1, service.Active[0].RawProgressOf(0),
                "O progresso congelou na conclusão.");
        }

        [Test]
        public void A_recompensa_sai_no_livro_razao_com_motivo_proprio()
        {
            // Sem motivo próprio a recompensa cairia em Unknown, e o relatório
            // diário mostraria "entrada desconhecida" no lugar de "objetivo".
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(1) },
                money: Money.FromYen(500));

            using ObjectiveService service = Service(definition);

            TransactionRecorded captured = default;
            using (_events.Subscribe<TransactionRecorded>(e =>
            {
                if (e.Reason == TransactionReason.ObjectiveReward) captured = e;
            }))
            {
                Sell(1);
                service.Flush();
            }

            Assert.AreEqual(TransactionReason.ObjectiveReward, captured.Reason);
            Assert.AreEqual(Money.FromYen(500), captured.Amount);
        }

        // ── o caso que motivou o Flush ───────────────────────────────────────

        [Test]
        public void Objetivo_que_ouve_transacoes_e_paga_em_dinheiro_nao_estoura()
        {
            // O EventBus tem guarda de recursão POR TIPO e LANÇA. Pagar a
            // recompensa dentro do despacho de TransactionRecorded, para um
            // objetivo que ouve TransactionRecorded, é publicar o mesmo tipo de
            // dentro dele mesmo. É por causa deste caso que a conclusão é
            // adiada, e não por elegância.
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<WatchTransactionsCondition>(1) },
                money: Money.FromYen(500));

            using ObjectiveService service = Service(definition);

            Assert.DoesNotThrow(() =>
            {
                _ledger.Deposit(Money.FromYen(10), TransactionReason.ProductSale, "venda");
                service.Flush();
            });

            Assert.IsTrue(service.Active[0].IsCompleted);
            Assert.AreEqual(Money.FromYen(1510), _ledger.Balance);
        }

        // ── desbloqueio e cadeia ─────────────────────────────────────────────

        [Test]
        public void Objetivo_travado_nao_entra_em_jogo()
        {
            ObjectiveDefinition travado = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(1) },
                unlock: Gate("abriu_a_loja"));

            using ObjectiveService service = Service(travado);

            Assert.AreEqual(0, service.Active.Count);
        }

        [Test]
        public void Concluir_um_objetivo_destrava_o_proximo_da_cadeia()
        {
            // A cadeia inteira, sem que nenhum dos dois assets conheça o outro:
            // o primeiro levanta a flag, o segundo tem um FlagUnlock apontando
            // para ela. O serviço não sabe que existe cadeia.
            ObjectiveDefinition primeiro = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(1) },
                flag: "primeira_venda", sortOrder: 0, name: "Primeira venda");

            ObjectiveDefinition segundo = Objective(
                new ObjectiveCondition[] { Condition<SurviveDaysCondition>(1) },
                unlock: Gate("primeira_venda"), sortOrder: 1, name: "Primeiro dia");

            using ObjectiveService service = Service(primeiro, segundo);

            Assert.AreEqual(1, service.Active.Count, "Só o primeiro está disponível.");

            Sell(1);
            service.Flush();

            Assert.AreEqual(2, service.Active.Count, "O segundo entrou ao concluir o primeiro.");
            Assert.AreSame(segundo, service.Active[1].Definition);
        }

        [Test]
        public void Subir_de_nivel_destrava_o_objetivo_sem_precisar_de_venda()
        {
            // O desbloqueio por nível não é reavaliado por progresso — se fosse,
            // um objetivo liberado no nível 5 só apareceria na venda seguinte.
            var gate = ScriptableObject.CreateInstance<StoreLevelUnlock>();
            gate.EditorSetRequiredLevel(2);
            _assets.Add(gate);

            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(1) },
                unlock: gate);

            using ObjectiveService service = Service(definition);
            Assert.AreEqual(0, service.Active.Count);

            _context.StoreLevel = 2;
            _level.AddXP(100);          // publica StoreLevelChanged
            service.Flush();

            Assert.AreEqual(1, service.Active.Count);
        }

        [Test]
        public void Chegar_ao_nivel_usa_o_nivel_atual_e_nao_a_soma_dos_avisos()
        {
            // Nível é ESTADO, não acumulado. Somar cada aviso faria o caminho
            // 1→2→3 valer 2+3 = 5, e "chegue ao nível 4" seria cumprido no
            // nível 3 — um objetivo inteiro pulado sem nada no console.
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<ReachStoreLevelCondition>(4) });

            using ObjectiveService service = Service(definition);

            _level.AddXP(100);              // nível 2
            service.Flush();
            Assert.IsFalse(service.Active[0].IsCompleted);

            _level.AddXP(150);              // nível 3 — somando daria 5, e concluiria
            service.Flush();
            Assert.IsFalse(service.Active[0].IsCompleted, "Nível 3 não é nível 4.");
            Assert.AreEqual(3, service.Active[0].RawProgressOf(0));

            _level.AddXP(200);              // nível 4
            service.Flush();
            Assert.IsTrue(service.Active[0].IsCompleted);
        }

        // ── teto ─────────────────────────────────────────────────────────────

        [Test]
        public void O_teto_de_objetivos_simultaneos_e_respeitado_exatamente()
        {
            // Um laço que ativa e SÓ ENTÃO pergunta se cabia deixa sempre um a
            // mais em jogo, e o teto de 2 vira 3 sem que nada acuse.
            ObjectiveDefinition a = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(1) }, sortOrder: 0);
            ObjectiveDefinition b = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(2) }, sortOrder: 1);
            ObjectiveDefinition c = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(3) }, sortOrder: 2);

            var service = new ObjectiveService(new FakeObjectiveCatalog(a, b, c), _events,
                                               _ledger, _level, _context, _context,
                                               maxActive: 2);

            Assert.AreEqual(2, service.Active.Count);
            Assert.AreSame(a, service.Active[0].Definition, "Menor SortOrder primeiro.");
            Assert.AreSame(b, service.Active[1].Definition);

            service.Dispose();
        }

        [Test]
        public void Concluir_abre_vaga_para_o_proximo()
        {
            ObjectiveDefinition a = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(1) }, sortOrder: 0);
            ObjectiveDefinition b = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(9) }, sortOrder: 1);

            var service = new ObjectiveService(new FakeObjectiveCatalog(a, b), _events,
                                               _ledger, _level, _context, _context,
                                               maxActive: 1);

            Assert.AreEqual(1, service.Active.Count);

            Sell(1);
            service.Flush();

            Assert.AreEqual(2, service.Active.Count);
            Assert.AreSame(b, service.Active[1].Definition);

            service.Dispose();
        }

        // ── assets mal preenchidos ───────────────────────────────────────────

        [Test]
        public void Objetivo_sem_condicao_nunca_entra_em_jogo()
        {
            // Sem condição ele estaria "cumprido" no instante em que aparece, e
            // pagaria a recompensa por nada. Pior que não existir, porque
            // ninguém percebe.
            ObjectiveDefinition vazio = Objective(new ObjectiveCondition[0],
                                                  money: Money.FromYen(9999));

            using ObjectiveService service = Service(vazio);
            service.Flush();

            Assert.AreEqual(0, service.Active.Count);
            Assert.AreEqual(Money.FromYen(1000), _ledger.Balance);
        }

        [Test]
        public void Objetivo_com_condicao_nula_nao_entra_em_jogo()
        {
            ObjectiveDefinition quebrado = Objective(new ObjectiveCondition[] { null },
                                                     money: Money.FromYen(9999));

            using ObjectiveService service = Service(quebrado);

            Assert.DoesNotThrow(() => { Sell(1); service.Flush(); });
            Assert.AreEqual(0, service.Active.Count);
            Assert.AreEqual(Money.FromYen(1000), _ledger.Balance);
        }

        [Test]
        public void Servico_sem_catalogo_nao_estoura()
        {
            var service = new ObjectiveService(null, _events, _ledger, _level, _context, _context);

            Assert.DoesNotThrow(() => { Sell(3); service.Flush(); });
            Assert.AreEqual(0, service.Active.Count);

            service.Dispose();
        }

        // ── save ─────────────────────────────────────────────────────────────

        [Test]
        public void O_progresso_sobrevive_ao_save()
        {
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(10) });

            var catalog = new FakeObjectiveCatalog(definition);

            var antes = new ObjectiveService(catalog, _events, _ledger, _level, _context, _context);
            Sell(4);

            var saved = new List<ObjectiveSnapshot>(antes.Snapshot());
            antes.Dispose();

            var depois = new ObjectiveService(catalog, _events, _ledger, _level,
                                              _context, _context);
            depois.Restore(saved);

            Assert.AreEqual(1, depois.Active.Count);
            Assert.AreEqual(4, depois.Active[0].RawProgressOf(0));

            Sell(6);
            depois.Flush();

            Assert.IsTrue(depois.Active[0].IsCompleted, "Continua de onde parou.");

            depois.Dispose();
        }

        [Test]
        public void Carregar_um_save_nao_paga_recompensa_de_novo()
        {
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(1) },
                money: Money.FromYen(500), xp: 40, flag: "pago");

            var catalog = new FakeObjectiveCatalog(definition);

            var antes = new ObjectiveService(catalog, _events, _ledger, _level, _context, _context);
            Sell(1);
            antes.Flush();

            var saved = new List<ObjectiveSnapshot>(antes.Snapshot());
            antes.Dispose();

            Money saldo = _ledger.Balance;
            int xp = _level.CurrentXP;
            int flags = _context.RaiseCount;

            var depois = new ObjectiveService(catalog, _events, _ledger, _level,
                                              _context, _context);
            depois.Restore(saved);
            depois.Flush();
            depois.Flush();

            Assert.IsTrue(depois.Active[0].IsCompleted);
            Assert.AreEqual(saldo, _ledger.Balance, "A recompensa não é paga duas vezes.");
            Assert.AreEqual(xp, _level.CurrentXP);
            Assert.AreEqual(flags, _context.RaiseCount);

            depois.Dispose();
        }

        [Test]
        public void Save_com_objetivo_que_nao_existe_mais_carrega_assim_mesmo()
        {
            // O objetivo foi removido do jogo entre duas versões. Derrubar o
            // carregamento por isso faria o jogador perder a partida por uma
            // mudança de conteúdo que não é problema dele.
            ObjectiveDefinition removido = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(3) });

            var catalog = new FakeObjectiveCatalog(removido);

            var antes = new ObjectiveService(catalog, _events, _ledger, _level, _context, _context);
            Sell(2);

            var saved = new List<ObjectiveSnapshot>(antes.Snapshot());
            antes.Dispose();

            catalog.Remove(removido);

            var depois = new ObjectiveService(catalog, _events, _ledger, _level,
                                              _context, _context);

            Assert.DoesNotThrow(() => depois.Restore(saved));
            Assert.AreEqual(0, depois.Active.Count);

            depois.Dispose();
        }

        [Test]
        public void Restore_nulo_nao_estoura_e_recomeca_do_zero()
        {
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(3) });

            using ObjectiveService service = Service(definition);
            Sell(2);

            Assert.DoesNotThrow(() => service.Restore(null));
            Assert.AreEqual(1, service.Active.Count);
            Assert.AreEqual(0, service.Active[0].RawProgressOf(0));
        }

        // ── derrubada ────────────────────────────────────────────────────────

        [Test]
        public void Depois_de_Dispose_o_servico_para_de_contar()
        {
            ObjectiveDefinition definition = Objective(
                new ObjectiveCondition[] { Condition<SellItemsCondition>(10) });

            var service = Service(definition);
            Sell(2);
            service.Dispose();

            Sell(50);

            Assert.AreEqual(2, service.Active[0].RawProgressOf(0));
            Assert.DoesNotThrow(() => service.Flush());
            Assert.DoesNotThrow(() => service.Dispose());
        }
    }
}
