using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    [TestFixture]
    public class StoreLevelServiceTests
    {
        private EventBus _bus;
        private StoreLevelService _service;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _service = new StoreLevelService(_bus);
        }

        [Test]
        public void StartsAtLevelOne_WithZeroXP()
        {
            Assert.AreEqual(1, _service.CurrentLevel);
            Assert.AreEqual(0, _service.CurrentXP);
        }

        [Test]
        public void AddsXp_OnSaleCompleted()
        {
            _bus.Publish(new SaleCompleted(1, default, Money.Zero, Money.Zero, 1, PaymentMethod.Cash));

            Assert.AreEqual(10, _service.CurrentXP, "Venda de 1 item dá 10 de XP.");
        }

        [Test]
        public void AddsBonusXp_ForMultipleItems()
        {
            _bus.Publish(new SaleCompleted(1, default, Money.Zero, Money.Zero, 3, PaymentMethod.Cash));

            Assert.AreEqual(14, _service.CurrentXP, "Venda de 3 itens: 10 + 2×2 = 14 de XP.");
        }

        [Test]
        public void LevelsUp_WhenXpThresholdReached()
        {
            int levelEvents = 0;
            int lastXpEvent = -1;

            _service.LevelChanged += (level) => levelEvents++;
            _service.XPChanged += (xp) => lastXpEvent = xp;

            _service.AddXP(100);

            Assert.AreEqual(2, _service.CurrentLevel);
            Assert.AreEqual(0, _service.CurrentXP, "O XP é descontado ao subir de nível.");
            Assert.AreEqual(1, levelEvents);
            Assert.AreEqual(0, lastXpEvent,
                "O último XPChanged mostra o que sobrou depois do desconto.");
        }

        [Test]
        public void LevelsUpMultipleTimes_IfXpIsHighEnough()
        {

            _service.AddXP(300);

            Assert.AreEqual(3, _service.CurrentLevel);
            Assert.AreEqual(50, _service.CurrentXP);
        }

        [Test]
        public void Restore_avisa_a_interface()
        {
            // A versão anterior restaurava em silêncio, e a barra de XP ficava
            // mostrando o estado de antes do save até a próxima venda.
            int level = -1, xp = -1;
            _service.LevelChanged += l => level = l;
            _service.XPChanged += x => xp = x;

            _service.Restore(5, 50);

            Assert.AreEqual(5, _service.CurrentLevel);
            Assert.AreEqual(50, _service.CurrentXP);
            Assert.AreEqual(5, level);
            Assert.AreEqual(50, xp);
        }

        [Test]
        public void Restore_avisa_UMA_vez_so()
        {
            // A reavaliação do nível avisava por conta própria e o Restore
            // avisava de novo no fim: dois LevelChanged por carregamento, e
            // quem usa esse evento para tocar a animação de subir de nível a
            // via duas vezes só por abrir o save.
            int levels = 0, xps = 0;
            _service.LevelChanged += _ => levels++;
            _service.XPChanged += _ => xps++;

            _service.Restore(1, 5000);

            Assert.AreEqual(1, levels);
            Assert.AreEqual(1, xps);
        }

        [Test]
        public void Restore_com_XP_de_sobra_reavalia_o_nivel_na_hora()
        {
            // Restore(1, 5000) deixava o jogador parado no nível 1 com XP de
            // sobra, e ele subia sete níveis de uma vez na venda seguinte.
            _service.Restore(1, 5000);

            Assert.Greater(_service.CurrentLevel, 1);
            Assert.Less(_service.CurrentXP, _service.GetXPForNextLevel());
        }

        [Test]
        public void Venda_sem_item_nao_da_XP()
        {
            _bus.Publish(new SaleCompleted(1, default, Money.Zero, Money.Zero, 0, PaymentMethod.Cash));

            Assert.AreEqual(0, _service.CurrentXP, "Venda anulada não é venda.");
        }

        [Test]
        public void A_barra_de_XP_chega_a_ver_o_valor_cheio_antes_de_virar()
        {
            // Avisar só depois do desconto faz a barra saltar de 90 para 0 sem
            // nunca ser vista cheia — a animação de encher e virar fica
            // impossível de fazer sem gambiarra.
            var seen = new System.Collections.Generic.List<int>();
            _service.XPChanged += seen.Add;

            _service.AddXP(100);

            CollectionAssert.Contains(seen, 100);
            Assert.AreEqual(0, seen[seen.Count - 1], "E termina no resto depois de subir.");
        }
    }
}

