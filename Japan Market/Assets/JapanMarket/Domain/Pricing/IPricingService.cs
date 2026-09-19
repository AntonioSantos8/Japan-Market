using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Uma linha da tela de Preços: custo anterior, custo atual, custo médio,
    /// preço de mercado, preço, desconto e lucro.
    ///
    /// Struct imutável: cada mudança produz um valor novo. Duas coisas aqui
    /// existem por causa de defeitos concretos e não devem ser "simplificadas"
    /// de volta:
    ///
    ///  • <see cref="TotalSpent"/>, e não só a média. A média recalculada a
    ///    partir da própria média JÁ ARREDONDADA, com divisão inteira, não
    ///    acumula erro devagar — ela CONGELA. `(m·n + c) / (n+1)` volta a `m`
    ///    sempre que `|c − m| &lt; n+1`, então com treze unidades acumuladas a
    ///    média para de andar e a margem fica errada para sempre.
    ///
    ///  • <see cref="HasCustomPrice"/> explícito. Deduzir "o jogador
    ///    precificou" de "existe entrada no dicionário" é falso: repor estoque
    ///    também cria entrada.
    /// </summary>
    public readonly struct PricingData
    {
        public readonly ItemDefinition Product;

        /// <summary>Preço definido pelo jogador. Só vale se <see cref="HasCustomPrice"/>.</summary>
        public readonly Money CustomPrice;

        public readonly bool HasCustomPrice;

        /// <summary>Custo unitário do pedido anterior. Zero antes da segunda compra.</summary>
        public readonly Money LastCost;

        /// <summary>Custo unitário do último pedido.</summary>
        public readonly Money CurrentCost;

        /// <summary>Total já gasto comprando este produto. É daqui que sai a média.</summary>
        public readonly Money TotalSpent;

        public readonly int UnitsInAverage;

        /// <summary>Dia da última alteração de preço. 0 = nunca alterado.</summary>
        public readonly int LastChangeDay;

        public PricingData(ItemDefinition product, Money customPrice, bool hasCustomPrice,
                           Money lastCost, Money currentCost, Money totalSpent,
                           int unitsInAverage, int lastChangeDay)
        {
            Product = product;
            CustomPrice = customPrice;
            HasCustomPrice = hasCustomPrice;
            LastCost = lastCost;
            CurrentCost = currentCost;
            TotalSpent = totalSpent;
            UnitsInAverage = unitsInAverage;
            LastChangeDay = lastChangeDay;
        }

        public static PricingData Initial(ItemDefinition product) =>
            new(product, Money.Zero, false, Money.Zero,
                product != null ? product.BaseCost : Money.Zero,
                Money.Zero, 0, 0);

        public Money MarketPrice => Product != null ? Product.MarketPrice : Money.Zero;

        /// <summary>
        /// Preço em vigor. DERIVADO, não congelado: sem preço do jogador, vale o
        /// de mercado lido do produto agora. Guardar uma cópia do preço de
        /// mercado dentro do registro faria toda mudança de balanceamento parar
        /// de valer para os produtos já comprados, em silêncio.
        /// </summary>
        public Money SellPrice => HasCustomPrice ? CustomPrice : MarketPrice;

        /// <summary>Média ponderada por unidade, recalculada do total gasto.</summary>
        public Money AverageCost =>
            UnitsInAverage > 0
                ? Money.FromYen(TotalSpent.Yen / (double)UnitsInAverage)
                : Money.Zero;

        /// <summary>
        /// Base de comparação para lucro: o custo médio, ou — antes da primeira
        /// compra — o custo de tabela. Nunca zero por omissão, porque um custo
        /// zero faria a margem parecer 100% num produto nunca comprado.
        /// </summary>
        public Money CostBasis =>
            UnitsInAverage > 0 ? AverageCost
            : Product != null ? Product.BaseCost
            : Money.Zero;

        public Money Profit => SellPrice - CostBasis;

        public float MarginPercent =>
            SellPrice.Yen == 0 ? 0f : (float)(Profit.Yen * 100.0 / SellPrice.Yen);

        /// <summary>
        /// Quanto abaixo do preço de mercado, em porcento — a coluna "Desconto".
        /// Negativo significa cobrando ACIMA do mercado, que é informação.
        /// </summary>
        public float DiscountPercent =>
            MarketPrice.Yen == 0 ? 0f
            : (float)((MarketPrice - SellPrice).Yen * 100.0 / MarketPrice.Yen);

        public bool IsAboveMarket => SellPrice > MarketPrice;

        /// <summary>O jogador já comprou ou precificou? É o filtro da tabela.</summary>
        public bool HasHistory => UnitsInAverage > 0 || HasCustomPrice;

        public PricingData WithSellPrice(Money price, int day)
        {
            // Preço negativo é sempre erro de digitação, e deixá-lo passar faria
            // a loja PAGAR o cliente para levar o produto.
            if (price.IsNegative) price = Money.Zero;

            return new PricingData(Product, price, true, LastCost, CurrentCost,
                                   TotalSpent, UnitsInAverage, day);
        }

        public PricingData WithoutSellPrice(int day) =>
            new(Product, Money.Zero, false, LastCost, CurrentCost,
                TotalSpent, UnitsInAverage, day);

        /// <summary>
        /// Registra uma compra: o TOTAL pago e quantas unidades vieram.
        ///
        /// O total, e não o custo unitário, porque o unitário é uma divisão
        /// arredondada e reconstituir o total a partir dele erra por caixa: uma
        /// caixa de ¥100 com 3 unidades dá ¥33 no unitário, e ¥33 × 3 = ¥99
        /// contra ¥100 que saíram do livro-razão — um ien por caixa, para
        /// sempre, com a tabela de Preços desencontrada da contabilidade.
        ///
        /// Com o <c>BoxCost</c> derivado de hoje a divisão volta exata e os dois
        /// caminhos empatam. A assinatura assume o total mesmo assim: a
        /// alternativa é um contrato que só está certo enquanto ninguém der
        /// preço próprio à caixa, e quebra em silêncio no dia em que alguém der.
        /// </summary>
        public PricingData WithRestock(Money totalPaid, int units)
        {
            if (units <= 0) return this;

            // Unitário só para EXIBIR. A média nunca passa por aqui: ela sai de
            // TotalSpent, que é exato.
            Money unitCost = Money.FromYen(totalPaid.Yen / (double)units);

            // Antes da primeira compra, CurrentCost é o custo de TABELA do
            // produto — um valor sintético que ninguém pagou. Promovê-lo a
            // "custo anterior" na primeira reposição faria a tela mostrar uma
            // compra que nunca houve, e a seta de variação apontar para baixo
            // sem que nada tivesse mudado.
            Money lastCost = UnitsInAverage == 0 ? Money.Zero
                           : CurrentCost == unitCost ? LastCost
                           : CurrentCost;

            return new PricingData(Product, CustomPrice, HasCustomPrice,
                                   lastCost, unitCost,
                                   TotalSpent + totalPaid,
                                   UnitsInAverage + units, LastChangeDay);
        }

        public override string ToString() =>
            Product == null ? "(sem produto)"
            : $"{Product.name}: preço {SellPrice}, custo médio {AverageCost}, lucro {Profit}";
    }

    /// <summary>
    /// Dono único do preço de venda de cada produto, e do histórico de custo que
    /// a tela de Preços mostra.
    ///
    /// Substitui o <c>GlobalPrices</c>, que guardava os preços num dicionário
    /// preenchido a partir de <c>Enum.GetValues(typeof(Items))</c> e devolvia 0
    /// para produto nunca precificado — um zero silencioso que fazia o NPC levar
    /// o produto e pagar ¥0. Aqui não existe preço 0 por omissão: enquanto o
    /// jogador não define um preço, vale o de mercado.
    /// </summary>
    public interface IPricingService
    {
        Money GetSellPrice(ItemDefinition product);
        PricingData GetPricingData(ItemDefinition product);
        bool HasCustomPrice(ItemDefinition product);

        void SetSellPrice(ItemDefinition product, Money price, int day);
        void ClearSellPrice(ItemDefinition product, int day);

        /// <summary>
        /// Registra o que foi pago numa compra. Chamado no momento do PEDIDO —
        /// que é quando o dinheiro sai, e é o que a coluna "custo atual" da tela
        /// de Preços significa.
        ///
        /// <paramref name="totalPaid"/> é o TOTAL da compra, não o custo de uma
        /// unidade: o unitário é arredondado e não reconstitui o total.
        /// </summary>
        void RecordRestock(ItemDefinition product, Money totalPaid, int units);

        /// <summary>
        /// A tabela da tela: só produtos com história. Reaproveitada entre
        /// leituras — leia agora, não guarde.
        /// </summary>
        IReadOnlyList<PricingData> Table { get; }

        event Action<ItemDefinition, Money> PriceChanged;
    }

    public sealed class PricingService : IPricingService
    {
        private readonly Dictionary<ItemDefinition, PricingData> _data = new();
        private readonly List<PricingData> _table = new();

        public event Action<ItemDefinition, Money> PriceChanged;

        public IReadOnlyList<PricingData> Table
        {
            get
            {
                _table.Clear();
                foreach (PricingData data in _data.Values)
                    if (data.HasHistory) _table.Add(data);

                return _table;
            }
        }

        public Money GetSellPrice(ItemDefinition product)
        {
            if (product == null) return Money.Zero;

            return _data.TryGetValue(product, out PricingData data)
                ? data.SellPrice
                : product.MarketPrice;
        }

        public PricingData GetPricingData(ItemDefinition product)
        {
            if (product == null) return default;

            return _data.TryGetValue(product, out PricingData data)
                ? data
                : PricingData.Initial(product);
        }

        /// <summary>
        /// Só é true se o JOGADOR definiu o preço. Deduzir isso de
        /// <c>_data.ContainsKey</c> era errado nos dois sentidos: repor estoque
        /// criava a entrada (produto passava a parecer precificado) e limpar o
        /// preço deixava a chave lá (a flag nunca voltava a false).
        /// </summary>
        public bool HasCustomPrice(ItemDefinition product) =>
            product != null
            && _data.TryGetValue(product, out PricingData data)
            && data.HasCustomPrice;

        public void SetSellPrice(ItemDefinition product, Money price, int day)
        {
            if (product == null) return;

            PricingData updated = GetPricingData(product).WithSellPrice(price, day);
            _data[product] = updated;

            PriceChanged?.Invoke(product, updated.SellPrice);
        }

        public void ClearSellPrice(ItemDefinition product, int day)
        {
            if (product == null) return;

            // Nunca precificado: não cria entrada e não avisa ninguém de uma
            // mudança que não houve.
            if (!_data.TryGetValue(product, out PricingData current)) return;
            if (!current.HasCustomPrice) return;

            PricingData updated = current.WithoutSellPrice(day);
            _data[product] = updated;

            PriceChanged?.Invoke(product, updated.SellPrice);
        }

        public void RecordRestock(ItemDefinition product, Money totalPaid, int units)
        {
            if (product == null || units <= 0) return;

            _data[product] = GetPricingData(product).WithRestock(totalPaid, units);
        }

        /// <summary>
        /// Restaura um save. Entradas sem produto são descartadas: um asset
        /// apagado do projeto vira um `Product` null no JSON, e indexar o
        /// dicionário com ele derrubaria o carregamento inteiro.
        /// </summary>
        public void Restore(IEnumerable<PricingData> savedData)
        {
            _data.Clear();
            if (savedData == null) return;

            foreach (PricingData data in savedData)
            {
                if (data.Product == null) continue;
                _data[data.Product] = data;
            }
        }
    }
}
