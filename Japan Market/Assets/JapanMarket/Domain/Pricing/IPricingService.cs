using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Dono único do preço de venda de cada produto.
    ///
    /// Substitui o <c>GlobalPrices</c>, que guardava os preços num dicionário
    /// preenchido a partir de <c>Enum.GetValues(typeof(Items))</c>, consultava
    /// com foreach linear, e devolvia 0 para produto nunca precificado — um
    /// zero silencioso que fazia o NPC levar o produto e pagar ¥0.
    ///
    /// Aqui não existe preço 0 por omissão: enquanto o jogador não define um
    /// preço, vale o de mercado.
    /// </summary>
    public interface IPricingService
    {
        Money GetSellPrice(ItemDefinition product);
        bool HasCustomPrice(ItemDefinition product);
        void SetSellPrice(ItemDefinition product, Money price);
        void ClearSellPrice(ItemDefinition product);

        /// <summary>Disparado quando o preço de um produto muda. A etiqueta assina isto.</summary>
        event System.Action<ItemDefinition, Money> PriceChanged;
    }

    /// <summary>
    /// Implementação mínima: guarda o que o jogador definir, e cai no preço de
    /// mercado para o resto.
    ///
    /// Não é um stub — é o comportamento correto de uma loja recém-aberta, e
    /// segue valendo depois. A Fase 6 acrescenta histórico de custo e margem
    /// por cima disto, sem mudar o contrato.
    /// </summary>
    public sealed class PricingService : IPricingService
    {
        private readonly System.Collections.Generic.Dictionary<ItemDefinition, Money> _prices = new();

        public event System.Action<ItemDefinition, Money> PriceChanged;

        public Money GetSellPrice(ItemDefinition product)
        {
            if (product == null) return Money.Zero;
            return _prices.TryGetValue(product, out Money price) ? price : product.MarketPrice;
        }

        public bool HasCustomPrice(ItemDefinition product) =>
            product != null && _prices.ContainsKey(product);

        public void SetSellPrice(ItemDefinition product, Money price)
        {
            if (product == null) return;

            _prices[product] = price;
            PriceChanged?.Invoke(product, price);
        }

        public void ClearSellPrice(ItemDefinition product)
        {
            if (product == null || !_prices.Remove(product)) return;
            PriceChanged?.Invoke(product, product.MarketPrice);
        }
    }
}
