using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    [CreateAssetMenu(fileName = "UnlockAtStoreLevel",
        menuName = "Japan Market/Unlock/Store Level", order = 40)]
    public sealed class StoreLevelUnlock : UnlockCondition
    {
        [Min(0)] [SerializeField] private int _requiredLevel = 1;

        public int RequiredLevel => _requiredLevel;

        public override bool IsSatisfied(IUnlockContext context) =>
            context != null && context.StoreLevel >= _requiredLevel;

        public override string Describe() => $"Nível da Loja Necessário: {_requiredLevel}";

#if UNITY_EDITOR
        /// <summary>Só o editor e os testes escrevem: em runtime a condição é dado.</summary>
        public void EditorSetRequiredLevel(int level) => _requiredLevel = level < 0 ? 0 : level;
#endif
    }
}

