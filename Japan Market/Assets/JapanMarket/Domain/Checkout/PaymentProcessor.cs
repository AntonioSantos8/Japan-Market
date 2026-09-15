using System.Collections.Generic;
using System.Collections.ObjectModel;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// As regras de pagamento, sem UI e sem tween.
    ///
    /// O ganho concreto de ter isto separado: o troco passa a ser comparado de
    /// forma EXATA. O código atual confere com
    /// <c>Mathf.Abs(giving - correctChange) &lt; 0.5f</c> — uma tolerância de
    /// meio iene que existe só para esconder erro de ponto flutuante. Com
    /// <see cref="Money"/> em inteiro, a comparação é ==, e o jogador que der
    /// ¥1 a mais é corrigido em vez de passar batido.
    /// </summary>
    public static class PaymentProcessor
    {
        /// <summary>
        /// As denominações do iene em circulação, da menor para a maior.
        ///
        /// Ficam aqui, e não num asset, porque não são conteúdo: são a moeda do
        /// jogo. Quando a tela de caixa da Fase 5b precisar de botões, ela lê
        /// esta lista em vez de repetir os valores no prefab.
        /// </summary>
        public static readonly IReadOnlyList<Money> JapaneseDenominations =
            new ReadOnlyCollection<Money>(new[]
            {
                Money.FromYen(1),    Money.FromYen(5),    Money.FromYen(10),
                Money.FromYen(50),   Money.FromYen(100),  Money.FromYen(500),
                Money.FromYen(1000), Money.FromYen(2000), Money.FromYen(5000),
                Money.FromYen(10000),
            });

        /// <summary>Troco devido. Nunca negativo.</summary>
        public static Money CalculateChange(Money total, Money tendered) =>
            Money.Max(Money.Zero, tendered - total);

        /// <summary>O jogador devolveu exatamente o troco certo?</summary>
        public static bool IsChangeCorrect(Money total, Money tendered, Money given) =>
            given == CalculateChange(total, tendered);

        /// <summary>O valor digitado na maquininha bate com o total?</summary>
        public static bool IsTypedAmountCorrect(Money total, Money typed) => typed == total;

        /// <summary>
        /// Quanto o cliente entrega em dinheiro.
        ///
        /// Escolhe a menor cédula da lista que cubra o total; se nenhuma cobre,
        /// combina as maiores. Um cliente que deve ¥730 entrega ¥1000, não
        /// ¥10000 — o que torna o minigame do troco jogável em vez de aleatório.
        /// </summary>
        public static Money RollTenderedAmount(Money total, IReadOnlyList<Money> denominations)
        {
            if (total <= Money.Zero) return Money.Zero;
            if (denominations == null || denominations.Count == 0) return total;

            Money best = Money.Zero;
            bool found = false;

            for (int i = 0; i < denominations.Count; i++)
            {
                Money note = denominations[i];
                if (note < total) continue;
                if (found && note >= best) continue;

                best = note;
                found = true;
            }

            if (found) return best;

            // Nenhuma cédula sozinha cobre: empilha a maior até passar do total.
            Money largest = Money.Zero;
            for (int i = 0; i < denominations.Count; i++)
                largest = Money.Max(largest, denominations[i]);

            if (largest <= Money.Zero) return total;

            Money tendered = Money.Zero;
            while (tendered < total) tendered += largest;

            return tendered;
        }
    }
}
