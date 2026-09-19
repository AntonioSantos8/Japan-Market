using System;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    [CreateAssetMenu(fileName = "UnlockAllOf",
        menuName = "Japan Market/Unlock/All Of", order = 41)]
    public sealed class AllOfUnlock : UnlockCondition
    {
        [SerializeField] private UnlockCondition[] _conditions;

        public override bool IsSatisfied(IUnlockContext context)
        {
            if (_conditions == null) return true;
            foreach (UnlockCondition c in _conditions)
                if (c != null && !c.IsSatisfied(context)) return false;
            return true;
        }

        public override string Describe()
        {
            if (_conditions == null || _conditions.Length == 0) return string.Empty;

            var parts = new System.Text.StringBuilder();
            foreach (UnlockCondition c in _conditions)
            {
                if (c == null) continue;
                if (parts.Length > 0) parts.Append(" · ");
                parts.Append(c.Describe());
            }
            return parts.ToString();
        }
    }
}

