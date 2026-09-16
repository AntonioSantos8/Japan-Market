using System.Collections.Generic;
using JapanMarket.Data;
using JapanMarket.Domain;

namespace JapanMarket.Gameplay
{
    /// <summary>
    /// Progresso da loja: nível, dia e flags de desbloqueio. É o que todo
    /// <c>UnlockCondition</c> do projeto consulta.
    ///
    /// O nível é LIDO do <see cref="IStoreLevelService"/>, não guardado aqui.
    /// Manter uma cópia própria e um <c>SetStoreLevel</c> para sincronizá-la
    /// significava que qualquer caminho que subisse o nível sem lembrar de
    /// chamar o setter deixava o desbloqueio um nível atrás — o jogador subia
    /// para 10 e o produto de nível 10 continuava travado até a próxima venda.
    /// Uma fonte da verdade só.
    ///
    /// As flags continuam aqui porque ainda não têm dono: a Fase 8 liga-as aos
    /// objetivos concluídos, e nada que consulta desbloqueio precisa mudar
    /// quando isso acontecer.
    /// </summary>
    public sealed class StoreProgress : IUnlockContext, IProgressFlags
    {
        private readonly IStoreLevelService _storeLevelService;
        private readonly HashSet<string> _flags = new();

        public StoreProgress(IStoreLevelService storeLevelService = null)
        {
            _storeLevelService = storeLevelService;
        }

        /// <summary>
        /// Nível 1 sem serviço: uma cena de teste sem GameContext completo não
        /// pode fazer TODO produto parecer travado.
        /// </summary>
        public int StoreLevel => _storeLevelService?.CurrentLevel ?? 1;
        public int CurrentDay { get; private set; } = 1;

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        public void SetDay(int day) => CurrentDay = day < 1 ? 1 : day;

        public void RaiseFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) _flags.Add(flag);
        }

        public IReadOnlyCollection<string> Flags => _flags;

        /// <summary>
        /// Substitui TODAS as flags por um save.
        ///
        /// Substitui, e não acrescenta: carregar um save por cima de uma partida
        /// em andamento tem que descartar o progresso da partida atual. Somar
        /// deixaria o jogador com desbloqueios de uma partida que ele abandonou,
        /// e nada na tela explicaria de onde vieram.
        /// </summary>
        public void RestoreFlags(IEnumerable<string> flags)
        {
            _flags.Clear();
            if (flags == null) return;

            foreach (string flag in flags)
                if (!string.IsNullOrWhiteSpace(flag)) _flags.Add(flag);
        }
    }
}
