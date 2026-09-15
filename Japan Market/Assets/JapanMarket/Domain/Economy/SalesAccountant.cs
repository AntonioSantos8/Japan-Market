using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// A seta "Checkout → Evento de Venda Concluída → Economia" do seu diagrama,
    /// como uma classe de dez linhas úteis.
    ///
    /// É pequena de propósito, e o tamanho é o argumento: o checkout não conhece
    /// o livro-razão, o livro-razão não conhece o checkout, e ninguém precisou
    /// de um singleton para os dois se encontrarem. Trocar "venda credita na
    /// hora" por "venda credita no fim do dia" é mexer só aqui.
    /// </summary>
    public sealed class SalesAccountant : IDisposable
    {
        private readonly ILedger _ledger;
        private IDisposable _subscription;

        public SalesAccountant(IEventBus events, ILedger ledger)
        {
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

            if (events == null) return;
            _subscription = events.Subscribe<SaleCompleted>(OnSale);
        }

        private void OnSale(SaleCompleted sale)
        {
            if (!sale.Revenue.IsPositive) return;

            _ledger.Deposit(sale.Revenue, TransactionReason.ProductSale,
                            $"{sale.ItemCount} item(ns)");
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
