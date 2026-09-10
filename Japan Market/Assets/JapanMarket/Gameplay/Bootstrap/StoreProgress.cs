using System.Collections.Generic;
using JapanMarket.Data;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Progresso da loja: nível, dia e flags de desbloqueio.
    ///
    /// Versão mínima para a fundação — o nível é escrito à mão por enquanto.
    /// A Fase 6 liga o nível aos Store Points ganhos em cada venda, e a Fase 8
    /// liga as flags aos objetivos concluídos. A interface não muda quando isso
    /// acontecer, então nada que consulta desbloqueio precisa ser reescrito.
    /// </summary>
    public sealed class StoreProgress : IUnlockContext
    {
        private readonly HashSet<string> _flags = new();

        public int StoreLevel { get; private set; } = 1;
        public int CurrentDay { get; private set; } = 1;

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        public void SetStoreLevel(int level) => StoreLevel = level < 1 ? 1 : level;
        public void SetDay(int day) => CurrentDay = day < 1 ? 1 : day;

        public void RaiseFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) _flags.Add(flag);
        }

        public IReadOnlyCollection<string> Flags => _flags;
    }
}
