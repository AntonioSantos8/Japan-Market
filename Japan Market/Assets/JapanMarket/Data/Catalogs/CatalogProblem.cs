using System.Collections.Generic;
using UnityEngine;

namespace JapanMarket.Data
{

    public readonly struct CatalogProblem
    {
        public readonly Object Asset;
        public readonly string Message;

        public CatalogProblem(Object asset, string message)
        {
            Asset = asset;
            Message = message;
        }

        public string AssetName => Asset != null ? Asset.name : "catalog";
    }

    public interface IValidatableCatalog
    {
        string CatalogName { get; }
        int EntryCount { get; }
        List<CatalogProblem> Validate();
    }

    public interface IEditableCatalog<TDefinition> : IValidatableCatalog
        where TDefinition : ScriptableObject
    {
#if UNITY_EDITOR

        bool EditorSetItems(List<TDefinition> items);
#endif
    }
}
