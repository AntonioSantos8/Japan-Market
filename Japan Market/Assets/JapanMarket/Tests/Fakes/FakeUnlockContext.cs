using System.Collections.Generic;
using JapanMarket.Data;

namespace JapanMarket.Tests
{
    /// <summary>
    /// Contexto de desbloqueio controlável pelo teste.
    ///
    /// Compartilhado, e não aninhado em cada fixture: mercado e banco precisam
    /// do mesmo dublê, e duas cópias de um fake divergem exatamente como duas
    /// cópias de código de produção.
    ///
    /// Implementa os DOIS lados da flag — leitura e escrita — de propósito, e é
    /// o que permite testar a cadeia de objetivos de verdade: o objetivo A
    /// levanta a flag por aqui, e o desbloqueio do objetivo B pergunta por ela
    /// pelo mesmo objeto. Dois fakes separados testariam os dois lados sem nunca
    /// testar a ligação entre eles — que é justamente onde mora o defeito.
    /// </summary>
    public sealed class FakeUnlockContext : IUnlockContext, IProgressFlags
    {
        private readonly HashSet<string> _flags = new();

        public int StoreLevel { get; set; } = 1;
        public int CurrentDay { get; set; } = 1;

        /// <summary>Quantas vezes RaiseFlag foi chamado, contando repetições.</summary>
        public int RaiseCount { get; private set; }

        public bool HasFlag(string flag) => _flags.Contains(flag);

        public void RaiseFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag)) return;

            RaiseCount++;
            _flags.Add(flag);
        }

        /// <summary>
        /// Derruba uma flag. O jogo não faz isso — mas carregar um save por cima
        /// de uma partida em andamento faz, e é o caminho por onde um slot de
        /// ferramenta já destravado volta a travar.
        /// </summary>
        public void ClearFlag(string flag) => _flags.Remove(flag);
    }
}
