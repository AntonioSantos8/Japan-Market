using System.Collections.Generic;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>
    /// Um problema encontrado na validação de um catálogo.
    ///
    /// A ideia é que um asset mal configurado apareça no import, e não semanas
    /// depois disfarçado de "esse item vende por ¥0" ou "esse móvel não spawna".
    /// </summary>
    public readonly struct CatalogProblem
    {
        public readonly Object Asset;
        public readonly string Message;

        public CatalogProblem(Object asset, string message)
        {
            Asset = asset;
            Message = message;
        }

        public string AssetName => Asset != null ? Asset.name : "catálogo";
    }

    /// <summary>
    /// Catálogo que sabe se autoverificar. Implementado por todo catálogo do
    /// projeto para que o <c>GameContext</c> e o menu do editor possam validar
    /// qualquer um deles sem conhecer o tipo concreto.
    /// </summary>
    public interface IValidatableCatalog
    {
        string CatalogName { get; }
        int EntryCount { get; }
        List<CatalogProblem> Validate();
    }

    /// <summary>
    /// Lado do editor: o catálogo é POPULADO pela ferramenta de import, nunca
    /// arrastado à mão. Separado da interface de leitura porque nada em runtime
    /// tem motivo para reescrever um catálogo.
    /// </summary>
    public interface IEditableCatalog<TDefinition> : IValidatableCatalog
        where TDefinition : ScriptableObject
    {
#if UNITY_EDITOR
        /// <summary>Devolve true se a lista realmente mudou.</summary>
        bool EditorSetItems(List<TDefinition> items);
#endif
    }
}
