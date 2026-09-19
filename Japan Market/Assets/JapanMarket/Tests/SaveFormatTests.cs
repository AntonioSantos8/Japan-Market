using JapanMarket.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JapanMarket.Tests
{
    /// <summary>
    /// O FORMATO do save, sem cena e sem disco.
    ///
    /// O que estes testes protegem é específico e vale o arquivo: o
    /// <c>JsonUtility</c> falha em SILÊNCIO. Ele não lança ao encontrar uma
    /// propriedade em vez de um campo, um Dictionary, uma interface ou um campo
    /// privado sem [SerializeField] — ele simplesmente não grava, e o save sai
    /// pela metade sem nada no console. Um round-trip que compara os valores é
    /// o único jeito de descobrir isso antes do jogador.
    ///
    /// A regra prática: todo campo novo no GameSave ganha uma linha aqui.
    /// </summary>
    [TestFixture]
    public class SaveFormatTests
    {
        private static GameSave Sample() => new()
        {
            Version = GameSave.CurrentVersion,
            SavedAtUtc = "2026-01-02T03:04:05.0000000Z",

            Clock = new ClockSave { Day = 7, TimeOfDay = 13.5f, StoreOpen = true },
            Ledger = new LedgerSave { BalanceYen = 12345 },

            Progress = new ProgressSave
            {
                StoreLevel = 4,
                StoreXP = 65,
                Flags = new[] { "primeira_venda", "comprou_taco" },
            },

            Pricing = new[]
            {
                new PricingSave
                {
                    ProductId = "abc123", HasCustomPrice = true, CustomPriceYen = 250,
                    LastCostYen = 90, CurrentCostYen = 100, TotalSpentYen = 2100,
                    UnitsInAverage = 26, LastChangeDay = 3,
                },
            },

            Market = new MarketSave
            {
                NextOrderId = 9,
                Pending = new[]
                {
                    new MarketOrderSave
                    {
                        Id = 8, DayPlaced = 7, ArrivesAtTotalHours = 155.25f,
                        Lines = new[]
                        {
                            new MarketLineSave
                            {
                                ProductId = "abc123", Boxes = 3,
                                BoxCostYen = 800, UnitsPerBox = 8,
                            },
                        },
                    },
                },
                Queue = new[] { new DeliveryLotSave { ProductId = "abc123", Boxes = 2 } },
            },

            Loans = new[] { new LoanSave { LoanKey = "inicial", PaymentsMade = 3 } },

            Objectives = new[]
            {
                new ObjectiveSave
                {
                    ObjectiveId = "obj-1", Progress = new[] { 4, 1 }, Completed = false,
                },
            },

            TrashDock = new[]
            {
                new TrashBagSave
                {
                    CategoryKey = "plastico",
                    ItemKeys = new[] { "garrafa-pet", "garrafa-pet", "embalagem" },
                },
            },

            Tools = new ToolsSave { UsesPerSlot = new[] { 12, 0, 5 }, SelectedIndex = 2 },
        };

        [Test]
        public void O_save_inteiro_sobrevive_a_ida_e_volta_pelo_JsonUtility()
        {
            GameSave original = Sample();

            string json = JsonUtility.ToJson(original);
            GameSave restored = JsonUtility.FromJson<GameSave>(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual(original.Version, restored.Version);
            Assert.AreEqual(original.SavedAtUtc, restored.SavedAtUtc);

            Assert.AreEqual(7, restored.Clock.Day);
            Assert.AreEqual(13.5f, restored.Clock.TimeOfDay, 0.0001f);
            Assert.IsTrue(restored.Clock.StoreOpen);

            Assert.AreEqual(12345, restored.Ledger.BalanceYen);

            Assert.AreEqual(4, restored.Progress.StoreLevel);
            Assert.AreEqual(65, restored.Progress.StoreXP);
            CollectionAssert.AreEqual(original.Progress.Flags, restored.Progress.Flags);

            Assert.AreEqual(1, restored.Pricing.Length);
            Assert.AreEqual("abc123", restored.Pricing[0].ProductId);
            Assert.AreEqual(2100, restored.Pricing[0].TotalSpentYen);
            Assert.AreEqual(26, restored.Pricing[0].UnitsInAverage);
            Assert.IsTrue(restored.Pricing[0].HasCustomPrice);

            Assert.AreEqual(9, restored.Market.NextOrderId);
            Assert.AreEqual(1, restored.Market.Pending.Length);
            Assert.AreEqual(155.25f, restored.Market.Pending[0].ArrivesAtTotalHours, 0.0001f);
            Assert.AreEqual(1, restored.Market.Pending[0].Lines.Length);
            Assert.AreEqual(800, restored.Market.Pending[0].Lines[0].BoxCostYen);
            Assert.AreEqual(2, restored.Market.Queue[0].Boxes);

            Assert.AreEqual("inicial", restored.Loans[0].LoanKey);
            Assert.AreEqual(3, restored.Loans[0].PaymentsMade);

            Assert.AreEqual("obj-1", restored.Objectives[0].ObjectiveId);
            CollectionAssert.AreEqual(new[] { 4, 1 }, restored.Objectives[0].Progress);

            Assert.AreEqual("plastico", restored.TrashDock[0].CategoryKey);
            Assert.AreEqual(3, restored.TrashDock[0].ItemKeys.Length);

            CollectionAssert.AreEqual(new[] { 12, 0, 5 }, restored.Tools.UsesPerSlot);
            Assert.AreEqual(2, restored.Tools.SelectedIndex);
        }

        [Test]
        public void Um_save_de_versao_anterior_carrega_com_os_campos_novos_vazios()
        {
            // O jogador atualiza o jogo com uma partida em andamento. Os blocos
            // que a versão antiga não gravava chegam nulos, e quem aplica trata
            // como "não havia" — nunca como exceção.
            const string antigo = @"{""Version"":1,""Clock"":{""Day"":3,""TimeOfDay"":9.0}}";

            GameSave save = JsonUtility.FromJson<GameSave>(antigo);

            Assert.IsNotNull(save);
            Assert.AreEqual(3, save.Clock.Day);
            Assert.AreEqual(9f, save.Clock.TimeOfDay, 0.0001f);

            // Os campos ausentes voltam com o valor de inicialização do campo, e
            // não nulos, porque o JsonUtility instancia os objetos do tipo. É
            // por isso que os arrays do GameSave nascem vazios em vez de nulos.
            Assert.IsNotNull(save.Pricing);
            Assert.AreEqual(0, save.Pricing.Length);
            Assert.IsNotNull(save.Market);
            Assert.IsNotNull(save.Tools);
        }

        [Test]
        public void Um_arquivo_que_nao_e_save_nao_passa_pela_checagem_de_versao()
        {
            // JSON válido que não é um save volta com tudo zerado, inclusive a
            // versão. É esse zero que o SaveFile usa para recusar o arquivo em
            // vez de aplicar um estado em branco por cima da partida.
            GameSave save = JsonUtility.FromJson<GameSave>(@"{""algumaCoisa"":1}");

            Assert.IsNotNull(save);
            Assert.AreEqual(0, save.Version);
        }

        [Test]
        public void O_dinheiro_viaja_como_inteiro_e_nao_perde_centavo()
        {
            // Long, e não Money nem float: um saldo de ¥2.147.483.648 num float
            // volta arredondado, e ninguém percebe até o jogador rico reclamar.
            var save = new GameSave { Ledger = new LedgerSave { BalanceYen = 9_007_199_254 } };

            GameSave restored = JsonUtility.FromJson<GameSave>(JsonUtility.ToJson(save));

            Assert.AreEqual(9_007_199_254L, restored.Ledger.BalanceYen);
        }
    }
}
