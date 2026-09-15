using System;
using System.Collections.Generic;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Uma venda em andamento.
    ///
    /// Repare no que NÃO existe aqui: dois contadores independentes. O
    /// CashRegister atual mantém <c>_totalExpected</c> e <c>_scannedCount</c>
    /// separados, e eles só ficam consistentes porque a animação da sacola dura
    /// 0,90 s enquanto os itens chegam a cada 0,20 s. Encurtar a animação
    /// quebraria o checkout — o fecho da venda depende de uma constante de
    /// tween.
    ///
    /// Aqui a sessão nasce com a lista completa de linhas. "Tudo passado" é
    /// <c>ScannedCount &gt;= Lines.Count</c>: uma condição sobre o estado real,
    /// que não depende da duração de nada.
    /// </summary>
    public sealed class CheckoutSession
    {
        private readonly List<SaleLine> _lines;
        private readonly List<SaleLine> _scanned = new();

        public CheckoutSession(ICustomer customer, IReadOnlyList<SaleLine> lines,
                               PaymentMethod method)
        {
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
            _lines = new List<SaleLine>(lines ?? throw new ArgumentNullException(nameof(lines)));
            Method = method;

            Total = Money.Zero;
            for (int i = 0; i < _lines.Count; i++) Total += _lines[i].Price;
        }

        public ICustomer Customer { get; }
        public IReadOnlyList<SaleLine> Lines => _lines;
        public IReadOnlyList<SaleLine> Scanned => _scanned;
        public PaymentMethod Method { get; }

        /// <summary>Valor a cobrar. Fixo desde a abertura da sessão.</summary>
        public Money Total { get; }

        /// <summary>Soma do que já foi passado pelo leitor.</summary>
        public Money ScannedTotal { get; private set; }

        public int ScannedCount => _scanned.Count;
        public int PendingCount => _lines.Count - _scanned.Count;
        public bool AllScanned => _scanned.Count >= _lines.Count;
        public bool IsComplete { get; private set; }

        /// <summary>Quanto o cliente entregou. Só faz sentido em dinheiro.</summary>
        public Money AmountTendered { get; private set; }

        public event Action<CheckoutSession, SaleLine> LineScanned;
        public event Action<CheckoutSession> AllLinesScanned;

        /// <summary>
        /// Registra que o jogador passou uma unidade pelo leitor.
        /// Devolve false se não havia mais nada para passar.
        /// </summary>
        public bool TryScanNext(out SaleLine line)
        {
            line = default;
            if (IsComplete || AllScanned) return false;

            line = _lines[_scanned.Count];
            _scanned.Add(line);
            ScannedTotal += line.Price;

            LineScanned?.Invoke(this, line);
            if (AllScanned) AllLinesScanned?.Invoke(this);

            return true;
        }

        public void SetAmountTendered(Money amount) => AmountTendered = amount;

        /// <summary>Troco devido. Zero se o cliente pagou exato ou com cartão.</summary>
        public Money ChangeDue => Method == PaymentMethod.Card
            ? Money.Zero
            : Money.Max(Money.Zero, AmountTendered - Total);

        internal void MarkComplete() => IsComplete = true;

        public override string ToString() =>
            $"Venda de {_lines.Count} item(ns), {Total}, {Method}, " +
            $"{(IsComplete ? "concluída" : $"{ScannedCount}/{_lines.Count} passados")}";
    }
}
