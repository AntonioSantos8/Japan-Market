using UnityEngine;

namespace JapanMarket.Data
{

    public interface IUnlockContext
    {
        int StoreLevel { get; }
        int CurrentDay { get; }
        bool HasFlag(string flag);
    }

    public abstract class UnlockCondition : ScriptableObject
    {
        public abstract bool IsSatisfied(IUnlockContext context);

        public abstract string Describe();
    }

    [CreateAssetMenu(fileName = "UnlockAtStoreLevel",
        menuName = "Japan Market/Unlock/Store Level", order = 40)]
    public sealed class StoreLevelUnlock : UnlockCondition
    {
        [Min(0)] [SerializeField] private int _requiredLevel = 1;

        public int RequiredLevel => _requiredLevel;

        public override bool IsSatisfied(IUnlockContext context) =>
            context != null && context.StoreLevel >= _requiredLevel;

        public override string Describe() => $"Store Level Required: {_requiredLevel}";
    }

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
