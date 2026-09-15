using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Uma unidade de produto numa venda, com o preço que o cliente viu na
    /// prateleira.
    ///
    /// O preço viaja junto com a linha, e não é recalculado no caixa, porque o
    /// jogador pode remarcar a etiqueta enquanto o cliente está na fila. Cobrar
    /// o preço novo faria o total mudar sozinho na frente do jogador.
    ///
    /// Existe para que o Domain do checkout fale de vendas sem conhecer a cesta
    /// do cliente, que é um MonoBehaviour.
    /// </summary>
    public readonly struct SaleLine
    {
        public readonly ItemDefinition Product;
        public readonly Money Price;

        public SaleLine(ItemDefinition product, Money price)
        {
            Product = product;
            Price = price;
        }

        /// <summary>
        /// Custo de aquisição desta unidade, para o lucro da tela de Preços.
        /// Zero se o produto foi removido do catálogo no meio da venda.
        /// </summary>
        public Money Cost => Product != null ? Product.BaseCost : Money.Zero;

        public override string ToString() =>
            Product != null ? $"{Product.name} · {Price}" : $"(produto removido) · {Price}";
    }
}
