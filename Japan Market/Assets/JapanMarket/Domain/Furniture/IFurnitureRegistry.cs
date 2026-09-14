using System;
using System.Collections.Generic;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Quem sabe quais móveis existem na loja agora.
    ///
    /// Substitui <c>FindObjectOfType</c>, <c>FindAnyObjectByType</c> e a lista
    /// <c>_placedFurnitures</c> pendurada no FurnitureManager. Três ganhos
    /// concretos:
    ///
    ///  • <see cref="WithCapability{T}"/> é O(1): as listas por capacidade são
    ///    mantidas incrementalmente no registro e na remoção, não filtradas a
    ///    cada consulta.
    ///
    ///  • <see cref="Removing"/> dispara ANTES de o móvel morrer. É esse aviso
    ///    que permite ao NPC na fila transicionar de estado em vez de acordar
    ///    com uma referência pendurada — o caso "caixa registradora sendo
    ///    removida" da sua lista de casos extremos.
    ///
    ///  • Ninguém guarda referência permanente a um móvel específico. Quem
    ///    precisa de um caixa pede um caixa; qual deles é problema do serviço.
    /// </summary>
    public interface IFurnitureRegistry
    {
        IReadOnlyList<IFurniture> All { get; }

        /// <summary>
        /// Todos os móveis vivos que expõem a capacidade pedida. A lista é
        /// mantida pelo registro — não aloca e não filtra a cada chamada.
        /// Não guarde a referência da lista entre frames: ela muda no lugar.
        /// </summary>
        IReadOnlyList<T> WithCapability<T>() where T : class, IFurnitureCapability;

        bool TryGetById(Core.FurnitureId id, out IFurniture furniture);

        /// <summary>Um móvel entrou na loja e já está pronto para uso.</summary>
        event Action<IFurniture> Placed;

        /// <summary>
        /// Um móvel está saindo. Disparado enquanto ele ainda é válido, para que
        /// quem depende dele consiga se desligar em ordem.
        /// </summary>
        event Action<IFurniture> Removing;

        void Register(IFurniture furniture);
        void Unregister(IFurniture furniture);
    }
}
