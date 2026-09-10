using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>Estado do jogo que uma condição de desbloqueio consulta.</summary>
    public interface IUnlockContext
    {
        int StoreLevel { get; }
        int CurrentDay { get; }
        bool HasFlag(string flag);
    }

    /// <summary>
    /// Condição para um produto, móvel ou ferramenta aparecer disponível.
    ///
    /// Uma condição nula significa "sempre disponível" — não existe um asset
    /// "AlwaysUnlocked" para arrastar em cada definição.
    ///
    /// Adicionar um tipo novo de desbloqueio é herdar desta classe. Nenhum
    /// sistema que consome desbloqueio precisa ser editado, porque ninguém
    /// pergunta "que tipo de condição é essa?" — só chama IsSatisfied.
    /// </summary>
    public abstract class UnlockCondition : ScriptableObject
    {
        public abstract bool IsSatisfied(IUnlockContext context);

        /// <summary>Texto para a UI: "Nível da Loja Necessário: 10".</summary>
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

        public override string Describe() => $"Nível da Loja Necessário: {_requiredLevel}";
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
