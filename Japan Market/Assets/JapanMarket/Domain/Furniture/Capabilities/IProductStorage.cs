using System;
using JapanMarket.Data;
using UnityEngine;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Guarda unidades de UM produto. Prateleira, freezer, geladeira, vitrine —
    /// todos são o mesmo componente com configuração diferente.
    ///
    /// O que muda em relação ao <c>Segment</c> atual: aqui não há animação, som,
    /// material, outline nem evento de tutorial. Só o estado do estoque e os
    /// avisos de mudança. Quem desenha assina <see cref="ContentsChanged"/> —
    /// é essa separação que faz o mesmo componente servir a um freezer com porta
    /// de vidro e a uma prateleira aberta sem nenhum ramo condicional.
    /// </summary>
    public interface IProductStorage : IFurnitureCapability
    {
        /// <summary>Condição que este móvel oferece. Um produto só entra se combinar.</summary>
        StorageTrait ProvidedStorage { get; }

        /// <summary>Produto guardado, ou null se vazio.</summary>
        ItemDefinition CurrentProduct { get; }

        int Count { get; }

        /// <summary>
        /// Quantas unidades cabem — do produto atual.
        ///
        /// Vale ZERO enquanto o móvel está vazio, porque a capacidade depende do
        /// tamanho do produto: uma prateleira cabe 8 ketchups ou 24 pacotes de
        /// biscoito. Para saber se algo cabe aqui, pergunte <see cref="Accepts"/>,
        /// nunca <c>Capacity &gt; 0</c>.
        /// </summary>
        int Capacity { get; }

        bool IsEmpty { get; }
        bool IsFull { get; }

        /// <summary>
        /// Aceita este produto? Falso se o trait não bate, se está cheio, ou se
        /// já guarda um produto diferente.
        /// </summary>
        bool Accepts(ItemDefinition product);

        bool TryPlace(ItemDefinition product, out int slotIndex);

        /// <summary>
        /// Retira uma unidade. É o que o NPC chama ao pegar o produto — e o que
        /// devolve false quando a prateleira esvaziou no meio da compra.
        /// </summary>
        bool TryTakeOne(out ItemDefinition product);

        Vector3 GetSlotWorldPosition(int slotIndex);

        /// <summary>Estoque mudou: quantidade, produto ou ambos.</summary>
        event Action<IProductStorage> ContentsChanged;
    }
}
