using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;
using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    /// <summary>
    /// A roda de ferramentas sem cena, sem roda e sem jogador.
    ///
    /// Se estes testes exigissem um prefab na mão e um raycast, a regra
    /// "a esponja não limpa vidro" seria verificável só jogando — e é
    /// exatamente o tipo de regra que quebra em silêncio quando alguém
    /// acrescenta a sexta ferramenta.
    /// </summary>
    [TestFixture]
    public class ToolBeltTests
    {
        private readonly List<Object> _assets = new();

        private ToolSurface _chao;
        private ToolSurface _vidro;
        private FakeUnlockContext _context;
        private EventBus _events;

        [SetUp]
        public void SetUp()
        {
            _events = new EventBus();
            _context = new FakeUnlockContext();

            _chao = Surface("Chão");
            _vidro = Surface("Vidro");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _assets.Count; i++)
                if (_assets[i] != null) Object.DestroyImmediate(_assets[i]);

            _assets.Clear();
        }

        private ToolSurface Surface(string name)
        {
            var surface = ScriptableObject.CreateInstance<ToolSurface>();
            surface.name = name;

            _assets.Add(surface);
            return surface;
        }

        private ToolDefinition Tool(string name, ToolSurface[] worksOn, int maxUses = 0,
                                    UnlockCondition unlock = null, float power = 1f)
        {
            var tool = ScriptableObject.CreateInstance<ToolDefinition>();
            tool.name = name;
            tool.EditorInitialize(worksOn, maxUses, unlock, power);

            _assets.Add(tool);
            return tool;
        }

        private ToolBeltLayout Layout(params ToolSlotLayout[] slots)
        {
            var layout = ScriptableObject.CreateInstance<ToolBeltLayout>();
            layout.EditorSetSlots(slots);

            _assets.Add(layout);
            return layout;
        }

        private FlagUnlock Gate(string flag)
        {
            var gate = ScriptableObject.CreateInstance<FlagUnlock>();
            gate.EditorSetFlag(flag);

            _assets.Add(gate);
            return gate;
        }

        private ToolBelt Belt(ToolBeltLayout layout) => new(layout, _context, _events);

        // ── superfícies ──────────────────────────────────────────────────────

        [Test]
        public void A_esponja_nao_limpa_vidro()
        {
            // A regra inteira do sistema, e ela não é um `if` em lugar nenhum: a
            // ferramenta diz onde trabalha, a sujeira diz onde está, e o cinto
            // cruza os dois.
            ToolDefinition esponja = Tool("Esponja", new[] { _chao });
            using ToolBelt belt = Belt(Layout(new ToolSlotLayout { Tool = esponja }));

            Assert.IsTrue(belt.TrySelect(0));

            Assert.AreEqual(ToolUseResult.Ok, belt.TryUse(_chao, out float power));
            Assert.AreEqual(1f, power);

            Assert.AreEqual(ToolUseResult.WrongSurface, belt.TryUse(_vidro, out power));
            Assert.AreEqual(0f, power, "Uso recusado não entrega força nenhuma.");
        }

        [Test]
        public void Ferramenta_sem_superficie_nenhuma_nao_limpa_nada()
        {
            // Tablet e engradado ocupam slot e são selecionáveis — só não limpam.
            ToolDefinition tablet = Tool("Tablet", new ToolSurface[0]);
            using ToolBelt belt = Belt(Layout(new ToolSlotLayout { Tool = tablet }));

            belt.TrySelect(0);

            Assert.AreEqual(ToolUseResult.WrongSurface, belt.TryUse(_chao, out _));
            Assert.AreEqual(ToolUseResult.WrongSurface, belt.TryUse(null, out _));
        }

        [Test]
        public void Sem_nada_selecionado_o_resultado_e_mao_vazia_e_nao_superficie_errada()
        {
            // A diferença importa na tela: "você não tem nada na mão" e "essa
            // ferramenta não serve aqui" pedem avisos diferentes.
            ToolDefinition esponja = Tool("Esponja", new[] { _chao });
            using ToolBelt belt = Belt(Layout(new ToolSlotLayout { Tool = esponja }));

            Assert.AreEqual(ToolUseResult.NoTool, belt.Check(_chao));
            Assert.AreEqual(ToolUseResult.NoTool, belt.TryUse(_chao, out _));
        }

        // ── desgaste ─────────────────────────────────────────────────────────

        [Test]
        public void A_ferramenta_gasta_e_quebra_no_uso_certo()
        {
            ToolDefinition esponja = Tool("Esponja", new[] { _chao }, maxUses: 3);
            using ToolBelt belt = Belt(Layout(new ToolSlotLayout { Tool = esponja }));

            belt.TrySelect(0);

            int broke = 0;
            belt.Broke += _ => broke++;

            Assert.AreEqual(3, belt.Slots[0].UsesLeft);

            belt.TryUse(_chao, out _);
            belt.TryUse(_chao, out _);
            Assert.AreEqual(1, belt.Slots[0].UsesLeft);
            Assert.AreEqual(0, broke);

            belt.TryUse(_chao, out _);

            Assert.IsTrue(belt.Slots[0].IsBroken);
            Assert.AreEqual(1, broke, "Avisa uma vez, no uso que quebrou.");

            Assert.AreEqual(ToolUseResult.Broken, belt.TryUse(_chao, out _));
            Assert.AreEqual(1, broke, "E não avisa de novo a cada tentativa.");
        }

        [Test]
        public void Errar_a_superficie_nao_gasta_a_ferramenta()
        {
            // Senão o jogador quebra a esponja esfregando vidro, e o custo do
            // erro vira dinheiro em vez de tempo.
            ToolDefinition esponja = Tool("Esponja", new[] { _chao }, maxUses: 2);
            using ToolBelt belt = Belt(Layout(new ToolSlotLayout { Tool = esponja }));

            belt.TrySelect(0);
            belt.TryUse(_vidro, out _);
            belt.TryUse(_vidro, out _);
            belt.TryUse(_vidro, out _);

            Assert.AreEqual(2, belt.Slots[0].UsesLeft);
            Assert.IsFalse(belt.Slots[0].IsBroken);
        }

        [Test]
        public void Ferramenta_sem_teto_de_usos_nunca_quebra()
        {
            // UsesLeft vale 0 a vida inteira numa ferramenta que não desgasta.
            // Confundir os dois deixaria o tablet quebrado de fábrica.
            ToolDefinition tablet = Tool("Tablet", new[] { _chao });
            using ToolBelt belt = Belt(Layout(new ToolSlotLayout { Tool = tablet }));

            belt.TrySelect(0);
            for (int i = 0; i < 50; i++) belt.TryUse(_chao, out _);

            Assert.IsFalse(belt.Slots[0].IsBroken);
            Assert.AreEqual(1f, belt.Slots[0].Condition);
        }

        [Test]
        public void Consertar_devolve_a_ferramenta_ao_uso()
        {
            ToolDefinition esponja = Tool("Esponja", new[] { _chao }, maxUses: 1);
            using ToolBelt belt = Belt(Layout(new ToolSlotLayout { Tool = esponja }));

            belt.TrySelect(0);
            belt.TryUse(_chao, out _);

            Assert.IsTrue(belt.Slots[0].IsBroken);
            Assert.IsTrue(belt.TryRepair(0));
            Assert.IsFalse(belt.Slots[0].IsBroken);

            Assert.IsFalse(belt.TryRepair(0), "Consertar o que está inteiro não faz nada.");
        }

        // ── slots travados ───────────────────────────────────────────────────

        [Test]
        public void Slot_travado_nao_pode_ser_selecionado()
        {
            ToolDefinition taco = Tool("Taco", new[] { _chao });
            using ToolBelt belt = Belt(Layout(
                new ToolSlotLayout { Tool = Tool("Esponja", new[] { _chao }) },
                new ToolSlotLayout { Tool = taco, Unlock = Gate("comprou_taco") }));

            Assert.IsFalse(belt.TrySelect(1));
            Assert.IsNull(belt.Selected);

            _context.RaiseFlag("comprou_taco");
            belt.Refresh();

            Assert.IsTrue(belt.TrySelect(1));
            Assert.AreSame(taco, belt.Selected.Tool);
        }

        [Test]
        public void O_slot_destrava_sozinho_quando_o_nivel_sobe()
        {
            var gate = ScriptableObject.CreateInstance<StoreLevelUnlock>();
            gate.EditorSetRequiredLevel(3);
            _assets.Add(gate);

            using ToolBelt belt = Belt(Layout(
                new ToolSlotLayout { Tool = Tool("Rodo", new[] { _vidro }), Unlock = gate }));

            int changes = 0;
            belt.SlotsChanged += () => changes++;

            Assert.IsFalse(belt.Slots[0].IsUnlocked(_context));

            _context.StoreLevel = 3;
            _events.Publish(new StoreLevelChanged(3, 0, 1));

            Assert.IsTrue(belt.Slots[0].IsUnlocked(_context));
            Assert.AreEqual(1, changes, "Avisa uma vez, e só quando muda de verdade.");

            _events.Publish(new StoreLevelChanged(3, 10, 0));
            Assert.AreEqual(1, changes, "Nada mudou: não redesenha a roda.");
        }

        [Test]
        public void Um_slot_que_trava_de_volta_solta_a_mao_do_jogador()
        {
            // O jogo não revoga flag, mas carregar um save por cima de uma
            // partida em andamento revoga. Ficar com uma ferramenta de slot
            // travado na mão é um estado que a roda não consegue desenhar.
            using ToolBelt belt = Belt(Layout(
                new ToolSlotLayout { Tool = Tool("Taco", new[] { _chao }),
                                     Unlock = Gate("comprou_taco") }));

            _context.RaiseFlag("comprou_taco");
            belt.Refresh();

            Assert.IsTrue(belt.TrySelect(0));
            Assert.IsNotNull(belt.Selected);

            ToolSlot handedBack = belt.Selected;
            belt.SelectionChanged += slot => handedBack = slot;

            _context.ClearFlag("comprou_taco");
            belt.Refresh();

            Assert.AreEqual(-1, belt.SelectedIndex);
            Assert.IsNull(belt.Selected);
            Assert.IsNull(handedBack, "E avisa quem estava segurando o modelo na mão.");
        }

        // ── save ─────────────────────────────────────────────────────────────

        [Test]
        public void O_desgaste_sobrevive_ao_save_e_respeita_o_teto_atual()
        {
            // O save é de uma versão em que a esponja durava 10. O balanceamento
            // baixou para 3 — vale a regra nova, senão o save vira um jeito de
            // burlar o desgaste.
            ToolDefinition esponja = Tool("Esponja", new[] { _chao }, maxUses: 3);
            using ToolBelt belt = Belt(Layout(new ToolSlotLayout { Tool = esponja }));

            belt.Restore(new[] { 10 }, selectedIndex: 0);

            Assert.AreEqual(3, belt.Slots[0].UsesLeft);
            Assert.AreEqual(0, belt.SelectedIndex);
        }

        [Test]
        public void Restore_nao_seleciona_slot_travado()
        {
            using ToolBelt belt = Belt(Layout(
                new ToolSlotLayout { Tool = Tool("Esponja", new[] { _chao }) },
                new ToolSlotLayout { Tool = Tool("Taco", new[] { _chao }),
                                     Unlock = Gate("comprou_taco") }));

            belt.Restore(new[] { 0, 0 }, selectedIndex: 1);

            Assert.AreEqual(-1, belt.SelectedIndex, "O save apontava para um slot travado.");
        }

        [Test]
        public void Cinto_sem_layout_e_um_jogador_de_maos_vazias_e_nao_um_erro()
        {
            using ToolBelt belt = new(null, _context, _events);

            Assert.AreEqual(0, belt.Slots.Count);
            Assert.IsFalse(belt.TrySelect(0));
            Assert.AreEqual(ToolUseResult.NoTool, belt.TryUse(_chao, out _));
            Assert.DoesNotThrow(() => belt.Refresh());
        }
    }

    /// <summary>O contador de sujeira, sem sujeira de verdade na cena.</summary>
    [TestFixture]
    public class CleanlinessTests
    {
        private sealed class Dirt : IDirtSource
        {
            public float Remaining => 1f;
        }

        [Test]
        public void A_mesma_sujeira_registrada_duas_vezes_conta_uma()
        {
            // Um OnEnable depois de um reparent registra de novo. Contar dobrado
            // faria a loja parecer o dobro de suja e espantar clientes por nada.
            var service = new CleanlinessService();
            var dirt = new Dirt();

            service.Register(dirt);
            service.Register(dirt);

            Assert.AreEqual(1, service.ActiveDirtCount);
        }

        [Test]
        public void O_evento_so_sai_quando_o_numero_muda()
        {
            var events = new EventBus();
            var service = new CleanlinessService(events);

            int published = 0;
            using (events.Subscribe<CleanlinessChanged>(_ => published++))
            {
                var a = new Dirt();
                service.Register(a);
                service.Register(a);          // repetido: nada muda
                service.Unregister(new Dirt()); // não estava lá: nada muda
                service.Unregister(a);
            }

            Assert.AreEqual(2, published, "Uma vez ao sujar, uma ao limpar.");
        }

        [Test]
        public void O_normalizado_satura_em_um()
        {
            var service = new CleanlinessService { ToleranceReference = 4 };

            for (int i = 0; i < 2; i++) service.Register(new Dirt());
            Assert.AreEqual(0.5f, service.Normalized, 0.001f);

            for (int i = 0; i < 20; i++) service.Register(new Dirt());
            Assert.AreEqual(1f, service.Normalized, 0.001f, "Não passa de 1.");
        }

        [Test]
        public void Clear_zera_entre_partidas()
        {
            // É o defeito do contador estático que este serviço substitui:
            // entrar em Play duas vezes deixava a loja permanentemente suja.
            var service = new CleanlinessService();
            for (int i = 0; i < 5; i++) service.Register(new Dirt());

            service.Clear();

            Assert.AreEqual(0, service.ActiveDirtCount);
            Assert.AreEqual(0f, service.Normalized);
        }
    }
}
