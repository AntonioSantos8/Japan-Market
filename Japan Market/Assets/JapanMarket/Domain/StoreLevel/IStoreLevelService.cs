using System;

namespace JapanMarket.Domain
{
    /// <summary>
    /// O nível da loja, que é o que destrava produto, móvel e faixa de
    /// empréstimo.
    ///
    /// Nasce de venda concluída e não de um campo escrito à mão: é a mesma
    /// ideia do resto do refatoramento — o número é consequência do que
    /// aconteceu, não um estado paralelo que alguém precisa lembrar de
    /// atualizar.
    /// </summary>
    public interface IStoreLevelService
    {
        int CurrentLevel { get; }
        int CurrentXP { get; }

        /// <summary>XP necessário para sair do nível atual.</summary>
        int GetXPForNextLevel();

        /// <summary>
        /// Concede XP fora de venda — objetivo concluído, tutorial, cheat.
        /// Está na interface de propósito: sem isso, quem resolve pelo container
        /// não consegue premiar nada (a Fase 8 precisa disto para objetivos).
        /// </summary>
        void AddXP(int amount);

        /// <summary>Restaura um save. Reavalia o nível e avisa a interface.</summary>
        void Restore(int level, int xp);

        event Action<int> LevelChanged;
        event Action<int> XPChanged;
    }
}
