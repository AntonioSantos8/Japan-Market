using System;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Móvel que recebe lixo: uma lixeira com um saco dentro.
    ///
    /// Capacidade, como prateleira e caixa. Uma lixeira é um prefab com
    /// <c>FurnitureInstance</c> + este componente, e nenhum sistema pergunta "é
    /// uma lixeira?" — pergunta-se
    /// <c>registry.WithCapability&lt;ITrashReceptacle&gt;()</c>.
    ///
    /// O ganho concreto disso aparece no lixo que o jogador carrega: ele não
    /// procura "a lixeira", procura um receptáculo que aceite a categoria que
    /// ele tem na mão. Uma lixeira nova na loja entra na conta sozinha.
    /// </summary>
    public interface ITrashReceptacle : IFurnitureCapability
    {
        /// <summary>O saco que está dentro. Nunca nulo enquanto a lixeira existe.</summary>
        TrashBag Bag { get; }

        /// <summary>
        /// Cheio: o jogador precisa tirar o saco antes de jogar mais lixo. É a
        /// regra do briefing — "depois de 10 lixos, tirar o lixo e vender".
        /// </summary>
        bool IsFull { get; }

        TrashSortResult TryDiscard(TrashDefinition trash);

        /// <summary>
        /// Retira o saco e põe um vazio no lugar. Devolve false com a lixeira
        /// vazia — tirar um saco sem nada dentro só daria trabalho ao jogador.
        /// </summary>
        bool TryTakeBag(out TrashBag bag);

        /// <summary>O conteúdo mudou: entrou lixo, ou o saco foi trocado.</summary>
        event Action<ITrashReceptacle> ContentsChanged;
    }
}
