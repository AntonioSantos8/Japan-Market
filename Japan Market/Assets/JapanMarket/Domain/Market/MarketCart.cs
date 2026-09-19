using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// O carrinho do app Mercado. C# puro e sem estado de tela: a mesma classe
    /// serve ao botão "+" da interface e a um teste que compra trinta produtos
    /// numa linha.
    ///
    /// Substitui o <c>ShopBuyItems</c>, que misturava carrinho, layout de botão
    /// e chamada de compra no mesmo MonoBehaviour — e por isso não existia
    /// resposta para "quanto está o carrinho?" sem uma cena aberta.
    /// </summary>
    public sealed class MarketCart
    {
        /// <summary>
        /// Teto por produto. Existe porque a quantidade vem de um campo de texto
        /// e uma caixa por unidade era alocada na entrega: sem limite, um
        /// 999999 digitado por engano congela a Unity.
        /// </summary>
        public const int MaxBoxesPerProduct = 999;

        private readonly Dictionary<ItemDefinition, int> _items = new();

        public IReadOnlyDictionary<ItemDefinition, int> Items => _items;

        /// <summary>Taxa única por pedido, independente da quantidade.</summary>
        public Money ShippingFee { get; set; }

        /// <summary>
        /// Outros itens cobrados junto do pedido, como móveis do computador.
        /// A tela é responsável por entregar esses itens depois do checkout.
        /// </summary>
        public Money AdditionalCost { get; set; }

        public Money TotalCost
        {
            get
            {
                Money total = TotalBoxes > 0 ? ShippingFee + AdditionalCost : Money.Zero;
                foreach (var kvp in _items)
                {
                    Money boxCost = kvp.Key.BoxCost;
                    long costYen = boxCost.Yen * kvp.Value;
                    total += Money.FromYen(costYen);
                }
                return total;
            }
        }

        public int TotalBoxes
        {
            get
            {
                int total = 0;
                foreach (var qty in _items.Values) total += qty;
                return total;
            }
        }

        public void AddBoxes(ItemDefinition product, int boxes)
        {
            if (product == null || boxes <= 0) return;

            _items.TryGetValue(product, out int current);
            _items[product] = Clamp(current + boxes);
        }

        public void RemoveBoxes(ItemDefinition product, int boxes)
        {
            if (product == null || boxes <= 0) return;

            if (_items.TryGetValue(product, out int current))
            {
                int newValue = current - boxes;
                if (newValue <= 0)
                    _items.Remove(product);
                else
                    _items[product] = newValue;
            }
        }

        public void SetBoxes(ItemDefinition product, int boxes)
        {
            if (product == null) return;

            if (boxes <= 0)
                _items.Remove(product);
            else
                _items[product] = Clamp(boxes);
        }

        private static int Clamp(int boxes) =>
            boxes > MaxBoxesPerProduct ? MaxBoxesPerProduct : boxes;

        public void Clear()
        {
            _items.Clear();
            ShippingFee = Money.Zero;
            AdditionalCost = Money.Zero;
        }
    }
}

