using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{

    public readonly struct SaleLine
    {
        public readonly ItemDefinition Product;
        public readonly Money Price;

        public SaleLine(ItemDefinition product, Money price)
        {
            Product = product;
            Price = price;
        }

        public Money Cost => Product != null ? Product.BaseCost : Money.Zero;

        public override string ToString() =>
            Product != null ? $"{Product.name} · {Price}" : $"(product removed) · {Price}";
    }
}
