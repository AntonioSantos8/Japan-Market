using JapanMarket.Core;
using NUnit.Framework;

namespace JapanMarket.Tests
{

    public sealed class MoneyTests
    {
        [Test]
        public void Zero_e_o_default()
        {
            Assert.AreEqual(Money.Zero, default(Money));
            Assert.IsTrue(Money.Zero.IsZero);
        }

        [Test]
        public void Soma_e_subtracao_sao_exatas()
        {
            Money total = Money.FromYen(730) + Money.FromYen(155) - Money.FromYen(85);
            Assert.AreEqual(800L, total.Yen);
        }

        [Test]
        public void Multiplicacao_por_inteiro_nao_perde_nada()
        {
            Assert.AreEqual(5840L, (Money.FromYen(730) * 8).Yen);
        }

        [Test]
        public void Desconto_arredonda_meio_para_cima()
        {

            Money discounted = Money.FromYen(155) * 0.85f;
            Assert.AreEqual(132L, discounted.Yen);
        }

        [Test]
        public void Comparacoes_seguem_o_valor()
        {
            Assert.IsTrue(Money.FromYen(100) > Money.FromYen(99));
            Assert.IsTrue(Money.FromYen(-1) < Money.Zero);
            Assert.AreEqual(Money.FromYen(50), Money.Min(Money.FromYen(50), Money.FromYen(80)));
            Assert.AreEqual(Money.FromYen(80), Money.Max(Money.FromYen(50), Money.FromYen(80)));
        }

        [Test]
        public void Troco_de_mil_operacoes_nao_acumula_erro()
        {

            Money balance = Money.Zero;
            for (int i = 0; i < 1000; i++)
            {
                balance += Money.FromYen(137);
                balance -= Money.FromYen(29);
            }
            Assert.AreEqual(108_000L, balance.Yen, "No tolerance should be necessary.");
        }

        [Test]
        public void Igualdade_de_troco_e_exata_sem_tolerancia()
        {
            Money paid   = Money.FromYen(1000);
            Money total  = Money.FromYen(367);
            Money change = paid - total;

            Money given = Money.FromYen(500) + Money.FromYen(100) + Money.FromYen(33);
            Assert.AreEqual(change, given, "Change matches without Mathf.Abs and without 0.5 margin.");
        }

        [Test]
        public void ToString_usa_o_simbolo_e_separador()
        {
            Assert.AreEqual("¥1,234", Money.FromYen(1234).ToString());
            Assert.AreEqual("¥1.23k", Money.FromYen(1234).ToCompactString());
            Assert.AreEqual("-¥1.23k", Money.FromYen(-1234).ToCompactString());
        }

        [TestCase("¥1,234", 1234L)]
        [TestCase("1234",   1234L)]
        [TestCase(" 1 234 ", 1234L)]
        [TestCase("-500",   -500L)]
        public void TryParse_aceita_o_que_o_jogador_digita(string input, long expected)
        {
            Assert.IsTrue(Money.TryParse(input, out Money parsed));
            Assert.AreEqual(expected, parsed.Yen);
        }

        [TestCase("abc")]
        [TestCase("")]
        [TestCase(null)]
        public void TryParse_recusa_lixo(string input)
        {
            Assert.IsFalse(Money.TryParse(input, out _));
        }
    }
}
