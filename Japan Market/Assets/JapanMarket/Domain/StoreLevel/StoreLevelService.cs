using System;
using JapanMarket.Core;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Implementação padrão. C# puro: assina o barramento e vai somando.
    /// </summary>
    public sealed class StoreLevelService : IStoreLevelService, IDisposable
    {
        private readonly IEventBus _events;
        private IDisposable _saleSubscription;

        public int CurrentLevel { get; private set; }
        public int CurrentXP { get; private set; }

        public event Action<int> LevelChanged;
        public event Action<int> XPChanged;

        public StoreLevelService(IEventBus events, int startingLevel = 1, int startingXP = 0)
        {
            _events = events;
            CurrentLevel = startingLevel > 0 ? startingLevel : 1;
            CurrentXP = startingXP > 0 ? startingXP : 0;

            if (_events != null)
                _saleSubscription = _events.Subscribe<SaleCompleted>(OnSaleCompleted);
        }

        /// <summary>
        /// Curva linear: 100, 150, 200… O <c>Math.Max</c> não é paranoia
        /// gratuita — é o que garante que o laço de subida de nível termina. Um
        /// save corrompido com nível absurdo estouraria o int e devolveria um
        /// valor negativo, e aí o `while` de CheckLevelUp nunca sairia.
        /// </summary>
        public int GetXPForNextLevel() => Math.Max(1, 100 + (50 * (CurrentLevel - 1)));

        /// <summary>
        /// XP por venda. Venda sem item não é venda — sem este guard, uma sessão
        /// anulada com zero linhas ainda daria os 10 de base.
        /// </summary>
        private void OnSaleCompleted(SaleCompleted sale)
        {
            if (sale.ItemCount <= 0) return;

            int xpEarned = 10 + (sale.ItemCount - 1) * 2;
            AddXP(xpEarned);
        }

        public void AddXP(int amount)
        {
            if (amount <= 0) return;

            CurrentXP += amount;

            // XPChanged ANTES de CheckLevelUp, com o valor cheio. Avisar só
            // depois do desconto faz a barra de progresso saltar de 90 para 0
            // sem nunca ser vista cheia — a animação de encher e virar fica
            // impossível de fazer sem gambiarra na UI.
            XPChanged?.Invoke(CurrentXP);

            int before = CurrentLevel;

            if (ApplyLevelUps())
            {
                LevelChanged?.Invoke(CurrentLevel);
                XPChanged?.Invoke(CurrentXP);   // o resto que sobrou depois de subir
            }

            Announce(CurrentLevel - before);
        }

        /// <summary>
        /// Um aviso só, no barramento, com o estado FINAL.
        ///
        /// Publicado depois dos eventos C#, e uma vez só mesmo quando o jogador
        /// sobe dois níveis de uma vez: quem conta "chegue ao nível 5" lê o
        /// nível que ficou, não a trajetória. Publicar durante a subida mandaria
        /// estados intermediários que nunca foram jogáveis.
        /// </summary>
        private void Announce(int levelsGained) =>
            _events?.Publish(new StoreLevelChanged(CurrentLevel, CurrentXP,
                                                   levelsGained < 0 ? 0 : levelsGained));

        /// <summary>
        /// Consome o XP e sobe os níveis que couberem. NÃO avisa ninguém: quem
        /// chama decide quando notificar, porque o <see cref="Restore"/> precisa
        /// avisar uma vez só, e um aviso emitido aqui dentro seria sempre o
        /// segundo.
        /// </summary>
        private bool ApplyLevelUps()
        {
            bool leveledUp = false;

            while (CurrentXP >= GetXPForNextLevel())
            {
                CurrentXP -= GetXPForNextLevel();
                CurrentLevel++;
                leveledUp = true;
            }

            return leveledUp;
        }

        /// <summary>
        /// Restaura um save. Reavalia o nível e avisa: a versão anterior aceitava
        /// `Restore(1, 5000)` em silêncio, deixava o jogador parado no nível 1
        /// com XP de sobra até a próxima venda — quando subia sete níveis de uma
        /// vez — e não avisava a interface em nenhum dos dois momentos.
        ///
        /// Avisa UMA vez, com o estado final. Disparar durante a reavaliação e
        /// outra vez no fim fazia a interface receber dois LevelChanged por
        /// carregamento, e quem usa esse evento para tocar a animação de subida
        /// de nível a via duas vezes ao abrir o save.
        /// </summary>
        public void Restore(int level, int xp)
        {
            CurrentLevel = level > 0 ? level : 1;
            CurrentXP = xp >= 0 ? xp : 0;

            ApplyLevelUps();

            LevelChanged?.Invoke(CurrentLevel);
            XPChanged?.Invoke(CurrentXP);

            // levelsGained = 0: carregar um save não é subir de nível. Quem toca
            // a animação de level up assina este evento, e ela não pode disparar
            // toda vez que o jogador abre a partida.
            Announce(0);
        }

        public void Dispose()
        {
            _saleSubscription?.Dispose();
            _saleSubscription = null;
        }
    }
}

