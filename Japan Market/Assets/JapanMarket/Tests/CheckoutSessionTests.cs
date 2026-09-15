using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Domain;
using NUnit.Framework;

namespace JapanMarket.Tests
{
    public sealed class CheckoutSessionTests
    {
        private static Money Y(long yen) => Money.FromYen(yen);

        private static List<SaleLine> Lines(params long[] prices)
        {
            var lines = new List<SaleLine>(prices.Length);
            foreach (long price in prices) lines.Add(new SaleLine(null, Y(price)));
            return lines;
        }

        [Test]
        public void Total_e_fixado_na_abertura()
        {
            var session = new CheckoutSession(new FakeCustomer(), Lines(155, 200, 90),
                                              PaymentMethod.Cash);

            Assert.AreEqual(Y(445), session.Total);
            Assert.AreEqual(3, session.PendingCount);
            Assert.IsFalse(session.AllScanned);
        }

        [Test]
        public void Tudo_passado_e_uma_condicao_sobre_o_estado_e_nao_um_contador()
        {

            var session = new CheckoutSession(new FakeCustomer(), Lines(155, 200),
                                              PaymentMethod.Cash);

            Assert.IsTrue(session.TryScanNext(out SaleLine first));
            Assert.AreEqual(Y(155), first.Price);
            Assert.IsFalse(session.AllScanned);

            Assert.IsTrue(session.TryScanNext(out _));
            Assert.IsTrue(session.AllScanned);
            Assert.AreEqual(Y(355), session.ScannedTotal);

            Assert.IsFalse(session.TryScanNext(out _), "There is nothing to scan after the last one.");
        }

        [Test]
        public void AllLinesScanned_dispara_uma_vez_so()
        {
            var session = new CheckoutSession(new FakeCustomer(), Lines(100, 100),
                                              PaymentMethod.Cash);

            int fired = 0;
            session.AllLinesScanned += _ => fired++;

            session.TryScanNext(out _);
            session.TryScanNext(out _);
            session.TryScanNext(out _);   

            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Troco_e_a_diferenca_no_dinheiro_e_zero_no_cartao()
        {
            var cash = new CheckoutSession(new FakeCustomer(), Lines(730), PaymentMethod.Cash);
            cash.SetAmountTendered(Y(1000));
            Assert.AreEqual(Y(270), cash.ChangeDue);

            var card = new CheckoutSession(new FakeCustomer(), Lines(730), PaymentMethod.Card);
            card.SetAmountTendered(Y(1000));
            Assert.AreEqual(Money.Zero, card.ChangeDue,
                "Card does not give change even if someone writes a value.");
        }

        [Test]
        public void Troco_nunca_e_negativo_se_o_cliente_pagou_a_menos()
        {
            var session = new CheckoutSession(new FakeCustomer(), Lines(1000), PaymentMethod.Cash);
            session.SetAmountTendered(Y(500));

            Assert.AreEqual(Money.Zero, session.ChangeDue);
        }

        [Test]
        public void Produto_apagado_do_projeto_nao_derruba_a_venda()
        {

            var session = new CheckoutSession(new FakeCustomer(), Lines(155),
                                              PaymentMethod.Cash);

            Assert.AreEqual(Y(155), session.Total);
            Assert.AreEqual(Money.Zero, session.Lines[0].Cost);
            Assert.DoesNotThrow(() => { string _ = session.Lines[0].ToString(); });
        }

        [Test]
        public void Venda_sem_cliente_ou_sem_linhas_e_rejeitada_na_construcao()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new CheckoutSession(null, Lines(100), PaymentMethod.Cash));

            Assert.Throws<System.ArgumentNullException>(
                () => new CheckoutSession(new FakeCustomer(), null, PaymentMethod.Cash));
        }

        [Test]
        public void Alterar_a_lista_de_origem_nao_muda_a_venda_aberta()
        {
            List<SaleLine> source = Lines(100, 100);
            var session = new CheckoutSession(new FakeCustomer(), source, PaymentMethod.Cash);

            source.Add(new SaleLine(null, Y(9999)));

            Assert.AreEqual(2, session.Lines.Count);
            Assert.AreEqual(Y(200), session.Total);
        }
    }
}
