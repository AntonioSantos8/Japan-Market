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




}

