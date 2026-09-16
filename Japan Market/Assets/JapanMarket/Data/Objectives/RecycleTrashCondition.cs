using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    [CreateAssetMenu(fileName = "RecycleTrash",
        menuName = "Japan Market/Objective/Reciclar lixo", order = 66)]
    public sealed class RecycleTrashCondition : ObjectiveCondition
    {
        [Tooltip("Chave da categoria (plastico, metal...). Vazio conta qualquer lixo.")]
        [SerializeField] private string _categoryKey;

        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<TrashDiscarded>(e =>
            {
                if (!Matches(e.CategoryKey)) return;

                progress.Add(1);
            });

        private bool Matches(string key) =>
            string.IsNullOrWhiteSpace(_categoryKey)
            || string.Equals(key, _categoryKey.Trim(), StringComparison.OrdinalIgnoreCase);

        public override string Describe() =>
            string.IsNullOrWhiteSpace(_categoryKey)
                ? $"Reciclar {Target} item(ns) de lixo"
                : $"Reciclar {Target} item(ns) de {_categoryKey}";

#if UNITY_EDITOR
        public void EditorSetCategoryKey(string key) => _categoryKey = key;
#endif
    }
}

