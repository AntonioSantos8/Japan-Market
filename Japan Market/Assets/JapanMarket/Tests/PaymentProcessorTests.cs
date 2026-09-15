using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    public sealed class PaymentProcessorTests
    {
        private static Money Y(long yen) => Money.FromYen(yen);

        [Test]
        public void Troco_e_a_diferenca()
        {
            Assert.AreEqual(Y(270), PaymentProcessor.CalculateChange(Y(730), Y(1000)));
        }

        [Test]
        public void Troco_nunca_e_negativo()
        {
            Assert.AreEqual(Money.Zero, PaymentProcessor.CalculateChange(Y(1000), Y(500)));
        }

        [Test]
        public void Um_iene_a_mais_ou_a_menos_esta_errado()
        {
            // O código atual compara com Mathf.Abs(diff) < 0.5f — uma tolerância
            // que existe só para esconder erro de ponto flutuante, e que deixa
            // passar troco errado. Com Money em inteiro, a comparação é exata.
            Assert.IsTrue(PaymentProcessor.IsChangeCorrect(Y(730), Y(1000), Y(270)));
            Assert.IsFalse(PaymentProcessor.IsChangeCorrect(Y(730), Y(1000), Y(271)));
            Assert.IsFalse(PaymentProcessor.IsChangeCorrect(Y(730), Y(1000), Y(269)));
        }

        [Test]
        public void Valor_digitado_tem_que_bater_exatamente()
        {
            Assert.IsTrue(PaymentProcessor.IsTypedAmountCorrect(Y(1240), Y(1240)));
            Assert.IsFalse(PaymentProcessor.IsTypedAmountCorrect(Y(1240), Y(1241)));
        }

        [Test]
        public void Cliente_entrega_a_menor_cedula_que_cobre()
        {
            // Jogável: quem deve ¥730 entrega ¥1000, não ¥10000.
            Assert.AreEqual(Y(1000),
                PaymentProcessor.RollTenderedAmount(Y(730),
                    PaymentProcessor.JapaneseDenominations));

            Assert.AreEqual(Y(100),
                PaymentProcessor.RollTenderedAmount(Y(64),
                    PaymentProcessor.JapaneseDenominations));
        }

        [Test]
        public void Valor_exato_de_uma_cedula_e_entregue_sem_troco()
        {
            Money tendered = PaymentProcessor.RollTenderedAmount(
                Y(1000), PaymentProcessor.JapaneseDenominations);

            Assert.AreEqual(Y(1000), tendered);
            Assert.AreEqual(Money.Zero, PaymentProcessor.CalculateChange(Y(1000), tendered));
        }

        [Test]
        public void Compra_maior_que_a_maior_cedula_empilha_notas()
        {
            Money total = Y(23500);

            Money tendered = PaymentProcessor.RollTenderedAmount(
                total, PaymentProcessor.JapaneseDenominations);

            Assert.GreaterOrEqual(tendered.CompareTo(total), 0, "Nunca entregar menos que o total.");
            Assert.AreEqual(Y(30000), tendered);
        }

        [Test]
        public void Sem_denominacoes_entrega_o_valor_exato()
        {
            Assert.AreEqual(Y(730), PaymentProcessor.RollTenderedAmount(Y(730), null));
            Assert.AreEqual(Y(730),
                PaymentProcessor.RollTenderedAmount(Y(730), new List<Money>()));
        }

        [Test]
        public void Total_zero_ou_negativo_nao_gera_entrega()
        {
            Assert.AreEqual(Money.Zero,
                PaymentProcessor.RollTenderedAmount(Money.Zero,
                    PaymentProcessor.JapaneseDenominations));
        }
    }
}
