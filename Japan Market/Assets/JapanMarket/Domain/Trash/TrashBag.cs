using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>Por que um lixo não entrou no saco. A tela precisa dizer qual.</summary>
    public enum TrashSortResult
    {
        Ok = 0,

        /// <summary>Lixo nulo, ou sem categoria — asset pela metade.</summary>
        NotRecyclable = 1,

        /// <summary>O saco já está separando outra categoria.</summary>
        WrongCategory = 2,

        /// <summary>Cheio. Tem que tirar e levar para os fundos.</summary>
        BagFull = 3,
    }

    /// <summary>
    /// Um saco de reciclagem: cabe um tanto, e de UMA categoria só.
    ///
    /// A categoria não é escolhida — ela é DECIDIDA PELO PRIMEIRO item que entra,
    /// e trava até o saco ser esvaziado. É essa trava que transforma "juntar
    /// lixo" em "separar lixo": o jogador precisa decidir a que destinar cada
    /// saco, e errar custa uma viagem.
    ///
    /// A alternativa — lixeira com categoria fixa no Inspector, como o
    /// <c>TrashBin</c> legado — empurra a decisão para o momento de construir a
    /// loja, e depois disso o jogo joga sozinho: cada lixo tem exatamente um
    /// lugar possível e não há escolha nenhuma a fazer.
    ///
    /// C# puro. Nenhum saco sabe que existe uma lixeira na cena.
    /// </summary>
    public sealed class TrashBag
    {
        public const int DefaultCapacity = 10;

        private readonly List<TrashDefinition> _items = new();

        public TrashBag(int capacity = DefaultCapacity)
        {
            Capacity = capacity < 1 ? 1 : capacity;
        }

        public int Capacity { get; }
        public int Count => _items.Count;

        /// <summary>A categoria travada, ou null enquanto o saco está vazio.</summary>
        public TrashCategory Category { get; private set; }

        public IReadOnlyList<TrashDefinition> Items => _items;

        public bool IsEmpty => _items.Count == 0;
        public bool IsFull => _items.Count >= Capacity;

        /// <summary>
        /// Quanto o caminhão paga por este saco.
        ///
        /// O bônus de saco cheio é somado aqui e não no serviço de propósito: é
        /// uma propriedade do saco, e a tela precisa mostrar o valor certo ANTES
        /// de o jogador levar até os fundos.
        /// </summary>
        public Money Value
        {
            get
            {
                Money total = Money.Zero;
                for (int i = 0; i < _items.Count; i++) total += _items[i].Value;

                if (IsFull && Category != null) total += Category.FullBagBonus;

                return total;
            }
        }

        public bool Accepts(TrashDefinition trash) => Check(trash) == TrashSortResult.Ok;

        /// <summary>
        /// Por que este lixo entraria ou não. Separado do <see cref="TryAdd"/>
        /// porque a mira do jogador precisa mostrar o motivo antes do clique.
        /// </summary>
        public TrashSortResult Check(TrashDefinition trash)
        {
            if (trash == null || !trash.IsValid) return TrashSortResult.NotRecyclable;
            if (IsFull) return TrashSortResult.BagFull;

            // `!=` em referência de asset, não comparação de chave: dois assets
            // com a mesma chave são erro de catálogo, e tratá-los como iguais
            // aqui esconderia o erro em vez de deixar o validador reclamar.
            if (Category != null && trash.Category != Category) return TrashSortResult.WrongCategory;

            return TrashSortResult.Ok;
        }

        public TrashSortResult TryAdd(TrashDefinition trash)
        {
            TrashSortResult result = Check(trash);
            if (result != TrashSortResult.Ok) return result;

            // A trava nasce aqui, no primeiro item, e não em Check: consultar
            // não pode decidir a categoria do saco.
            //
            // `== null` e não `??=`. O operador de coalescência é do C# puro e
            // passa por cima da sobrecarga de `==` da Unity: com um asset de
            // categoria destruído, o `Check` acima o trataria como nulo (e
            // aceitaria qualquer lixo) enquanto o `??=` o consideraria
            // preenchido e nunca o trocaria. O saco ficaria travado num estado
            // que nenhuma das duas leituras descreve.
            if (Category == null) Category = trash.Category;

            _items.Add(trash);

            return TrashSortResult.Ok;
        }

        public void Clear()
        {
            _items.Clear();
            Category = null;
        }

        /// <summary>
        /// Restaura um saco do save: categoria, quantidade e os itens que ainda
        /// existem no catálogo.
        ///
        /// Itens nulos são descartados e a CONTAGEM é preservada com o que
        /// sobrou — um lixo apagado do projeto entre duas versões vale zero, mas
        /// não some o saco inteiro nem trava a lixeira num estado impossível.
        /// </summary>
        public void Restore(TrashCategory category, IEnumerable<TrashDefinition> items)
        {
            _items.Clear();

            // Normalizado para nulo de verdade: uma categoria apagada do projeto
            // volta do save como o "null falso" da Unity, e guardá-la assim faria
            // o saco ficar travado numa categoria que não existe mais.
            Category = category != null ? category : null;

            if (items == null) return;

            foreach (TrashDefinition item in items)
            {
                if (item == null || !item.IsValid) continue;
                if (_items.Count >= Capacity) break;
                if (Category != null && item.Category != Category) continue;

                _items.Add(item);
            }

            // Saco restaurado vazio não fica com categoria travada: seria uma
            // lixeira que recusa tudo sem nada visível explicando.
            if (_items.Count == 0) Category = null;
        }

        public override string ToString() =>
            IsEmpty
                ? $"Saco vazio (0/{Capacity})"
                : $"{Category}: {Count}/{Capacity}, {Value}{(IsFull ? " — cheio" : "")}";
    }
}
