using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Domain
{
    /// <summary>
    /// Implementação padrão da lista de objetivos. C# puro.
    /// </summary>
    public sealed class ObjectiveService : IObjectiveService, IDisposable
    {
        /// <summary>
        /// Teto de rodadas de conclusão encadeada dentro de um Flush.
        ///
        /// Concluir levanta uma flag, a flag desbloqueia outro objetivo, e o
        /// outro pode já estar cumprido — legítimo, e é como uma cadeia de
        /// tutorial anda. O teto existe porque dois objetivos que se
        /// desbloqueiam em círculo travariam o jogo num laço sem fim, e um
        /// congelamento sem mensagem é o pior resultado possível.
        /// </summary>
        private const int MaxChainedCompletions = 16;

        private readonly IObjectiveCatalog _catalog;
        private readonly IEventBus _events;
        private readonly ILedger _ledger;
        private readonly IStoreLevelService _storeLevel;
        private readonly IUnlockContext _unlocks;
        private readonly IProgressFlags _flags;

        private readonly List<ActiveObjective> _active = new();

        /// <summary>
        /// Objetivos que JÁ entraram nesta partida, concluídos ou não. É o que
        /// impede um objetivo concluído de ser reativado pelo Refresh e pagar a
        /// recompensa de novo — a lista de ativos não serve para isso, porque um
        /// dia a UI vai querer tirar os concluídos dela.
        /// </summary>
        private readonly HashSet<ObjectiveId> _known = new();

        private readonly List<ObjectiveSnapshot> _snapshot = new();
        private readonly List<ActiveObjective> _ready = new();
        private readonly List<IDisposable> _busSubscriptions = new();

        private bool _dirty;
        private bool _flushing;
        private bool _disposed;

        /// <summary>
        /// <paramref name="maxActive"/> é parâmetro de construtor, e não só
        /// propriedade: o construtor já ativa os objetivos disponíveis, e um
        /// inicializador de objeto (<c>new ObjectiveService(...) { MaxActive = 2 }</c>)
        /// roda DEPOIS dele. O teto chegaria tarde, com um objetivo a mais já em
        /// jogo e nada indicando o porquê.
        /// </summary>
        public ObjectiveService(IObjectiveCatalog catalog, IEventBus events,
                                ILedger ledger = null, IStoreLevelService storeLevel = null,
                                IUnlockContext unlocks = null, IProgressFlags flags = null,
                                int maxActive = 3)
        {
            _maxActive = maxActive;
            _catalog = catalog;
            _events = events;
            _ledger = ledger;
            _storeLevel = storeLevel;
            _unlocks = unlocks;
            _flags = flags;

            // Os dois momentos em que um desbloqueio pode ter passado a valer
            // sem que nenhum objetivo tenha progredido. Sem isto, um objetivo
            // liberado no dia 3 só apareceria na próxima venda.
            if (_events != null)
            {
                _busSubscriptions.Add(_events.Subscribe<DayStarted>(_ => _dirty = true));
                _busSubscriptions.Add(_events.Subscribe<StoreLevelChanged>(_ => _dirty = true));
            }

            Refresh();
        }

        public IReadOnlyList<ActiveObjective> Active => _active;

        /// <summary>
        /// Aumentar o teto em jogo libera vaga; o objetivo novo entra no Flush
        /// seguinte, e não na hora. Diminuir NÃO tira objetivo de jogo — quem já
        /// está valendo continua até concluir, porque apagar o progresso do
        /// jogador por causa de uma mudança de configuração é pior que passar do
        /// teto por um tempo.
        /// </summary>
        public int MaxActive
        {
            get => _maxActive;
            set
            {
                if (_maxActive == value) return;

                _maxActive = value;
                _dirty = true;
            }
        }

        private int _maxActive;

        public event Action<ActiveObjective> ObjectiveStarted;
        public event Action<ActiveObjective> ObjectiveCompleted;
        public event Action ProgressChanged;

        // ── ativação ─────────────────────────────────────────────────────────

        /// <summary>
        /// Coloca em jogo os objetivos que desbloquearam e cabem no teto.
        ///
        /// Ordena por SortOrder para que a cadeia de tutorial apareça na ordem
        /// que o designer escreveu, e não na ordem em que o import varreu a
        /// pasta.
        /// </summary>
        private void Refresh()
        {
            if (_catalog == null) return;

            // A capacidade é checada ANTES de cada ativação, nunca depois: um laço
            // que ativa e só então pergunta se cabia deixa sempre um a mais em
            // jogo, e o teto de 3 vira 4 sem que nada acuse.
            while (!AtCapacity)
            {
                ObjectiveDefinition next = FindNextUnlocked();
                if (next == null) return;

                Start(next);
            }
        }

        /// <summary>
        /// O próximo a entrar: menor SortOrder entre os desbloqueados que ainda
        /// não entraram. Varredura linear porque o catálogo tem dezenas de
        /// entradas e isto roda no máximo uma vez por frame — um índice ordenado
        /// seria otimização sem problema medido.
        /// </summary>
        private ObjectiveDefinition FindNextUnlocked()
        {
            IReadOnlyList<ObjectiveDefinition> all = _catalog.All;
            if (all == null) return null;

            ObjectiveDefinition next = null;

            for (int i = 0; i < all.Count; i++)
            {
                ObjectiveDefinition candidate = all[i];

                if (candidate == null) continue;
                if (_known.Contains(candidate.Id)) continue;
                if (!candidate.IsValid) continue;
                if (!candidate.IsUnlocked(_unlocks)) continue;

                if (next == null || candidate.SortOrder < next.SortOrder) next = candidate;
            }

            return next;
        }

        private bool AtCapacity
        {
            get
            {
                if (MaxActive <= 0) return false;

                int running = 0;
                for (int i = 0; i < _active.Count; i++)
                    if (!_active[i].IsCompleted) running++;

                return running >= MaxActive;
            }
        }

        private void Start(ObjectiveDefinition definition)
        {
            var objective = new ActiveObjective(definition);
            _known.Add(definition.Id);
            _active.Add(objective);

            IReadOnlyList<ObjectiveCondition> conditions = definition.Conditions;
            for (int i = 0; i < conditions.Count; i++)
            {
                IObjectiveProgress slot = objective.Slot(i, MarkDirty);
                objective.Track(conditions[i].Watch(_events, slot));
            }

            ObjectiveStarted?.Invoke(objective);

            // Já podia estar cumprido: uma condição de estado ("chegue ao nível
            // 5") num jogador que já está no nível 7 nunca receberia um evento,
            // porque o evento que ela ouve já passou. O Flush resolve olhando o
            // estado, e não só o fluxo.
            _dirty = true;
        }

        private void MarkDirty()
        {
            _dirty = true;
            ProgressChanged?.Invoke();
        }

        // ── conclusão ────────────────────────────────────────────────────────

        public void Flush()
        {
            // Reentrância: pagar a recompensa publica BalanceChanged, e um
            // assinante disso pode chamar Flush de volta. A varredura de fora
            // termina o serviço.
            if (_disposed || _flushing) return;

            _flushing = true;

            try
            {
                // Incondicional, e não atrás do _dirty: uma flag pode ter sido
                // levantada por fora — o tutorial, um upgrade, um save — e
                // ninguém avisou este serviço. Amarrar a reavaliação ao
                // progresso faria um objetivo liberado por flag só aparecer na
                // próxima venda, ou nunca, num dia sem vendas. É uma varredura
                // linear de algumas dezenas de assets por frame; medir antes de
                // trocar isso por um índice.
                Refresh();

                if (!_dirty) return;
                _dirty = false;

                int rounds = 0;
                while (true)
                {
                    CollectReady();
                    if (_ready.Count == 0) return;

                    if (++rounds > MaxChainedCompletions)
                    {
                        // Rearmado: o que ficou para trás está cumprido e não
                        // pago, e sem isto nunca mais seria olhado.
                        _dirty = true;

                        UnityEngine.Debug.LogError(
                            "[Objetivos] Cadeia de conclusões longa demais num frame só. " +
                            "Provável ciclo: um objetivo levanta a flag que desbloqueia " +
                            "outro que levanta a flag do primeiro. A cadeia foi cortada — " +
                            "o resto conclui no frame seguinte.");
                        return;
                    }

                    for (int i = 0; i < _ready.Count; i++) Complete(_ready[i]);

                    // Concluir pode ter levantado flag e liberado o próximo da
                    // cadeia. Se não liberou, o CollectReady do topo volta vazio
                    // e o laço acaba.
                    Refresh();
                }
            }
            finally
            {
                _ready.Clear();
                _flushing = false;
            }
        }

        private void CollectReady()
        {
            _ready.Clear();

            for (int i = 0; i < _active.Count; i++)
            {
                ActiveObjective objective = _active[i];
                if (objective.IsCompleted || !objective.AllConditionsMet) continue;

                _ready.Add(objective);
            }
        }

        private void Complete(ActiveObjective objective)
        {
            // Marcado ANTES de pagar. Pagar publica eventos, um assinante pode
            // chamar Flush, e o MESMO objetivo não pode pagar duas vezes — um
            // dinheiro duplicado no caixa é o tipo de bug que o jogador não
            // reporta, ele só farma.
            objective.MarkCompleted();
            objective.Unsubscribe();

            ObjectiveReward reward = objective.Definition.Reward;
            string title = objective.Definition.Title.IsEmpty
                ? objective.Definition.name
                : objective.Definition.Title.Value;

            if (reward.HasMoney)
                _ledger?.Deposit(reward.Money, TransactionReason.ObjectiveReward,
                                 $"Objetivo: {title}");

            if (reward.HasXP) _storeLevel?.AddXP(reward.XP);
            if (reward.HasFlag) _flags?.RaiseFlag(reward.Flag);

            ObjectiveCompleted?.Invoke(objective);

            // Qualificado até o namespace: dentro desta classe o nome simples
            // `ObjectiveCompleted` é o evento C# logo acima, não o struct do
            // barramento. Os dois merecem o mesmo nome do ponto de vista de quem
            // assina, e é aqui dentro que a ambiguidade precisa ser resolvida.
            _events?.Publish(new JapanMarket.Core.ObjectiveCompleted(
                objective.Definition.Id, title, reward.Money, reward.XP));
        }

        // ── save ─────────────────────────────────────────────────────────────

        public IReadOnlyList<ObjectiveSnapshot> Snapshot()
        {
            _snapshot.Clear();

            for (int i = 0; i < _active.Count; i++)
            {
                ActiveObjective objective = _active[i];

                var progress = new int[objective.ConditionCount];
                for (int c = 0; c < progress.Length; c++) progress[c] = objective.RawProgressOf(c);

                _snapshot.Add(new ObjectiveSnapshot(objective.Definition.Id, progress,
                                                    objective.IsCompleted));
            }

            return _snapshot;
        }

        /// <summary>
        /// Restaura um save.
        ///
        /// Um id que não existe mais no catálogo é DESCARTADO em silêncio: o
        /// objetivo foi removido do jogo entre duas versões, e derrubar o
        /// carregamento por causa disso faria o jogador perder a partida por uma
        /// mudança de conteúdo que não é problema dele.
        ///
        /// Um objetivo cuja definição mudou de número de condições também é
        /// tolerado — o progresso é copiado até onde as duas listas coincidem.
        /// </summary>
        public void Restore(IEnumerable<ObjectiveSnapshot> saved)
        {
            for (int i = 0; i < _active.Count; i++) _active[i].Unsubscribe();

            _active.Clear();
            _known.Clear();

            if (saved != null && _catalog != null)
            {
                foreach (ObjectiveSnapshot entry in saved)
                {
                    if (!_catalog.TryGet(entry.Id, out ObjectiveDefinition definition)) continue;
                    if (definition == null || !definition.IsValid) continue;
                    if (_known.Contains(entry.Id)) continue;

                    var objective = new ActiveObjective(definition);
                    objective.RestoreProgress(entry.Progress, entry.Completed);

                    _known.Add(entry.Id);
                    _active.Add(objective);

                    // Concluído não volta a ouvir o barramento: seria trabalho por
                    // venda, para sempre, sem poder mudar nada.
                    if (entry.Completed) continue;

                    IReadOnlyList<ObjectiveCondition> conditions = definition.Conditions;
                    for (int c = 0; c < conditions.Count; c++)
                        objective.Track(conditions[c].Watch(_events, objective.Slot(c, MarkDirty)));
                }
            }

            Refresh();

            // NÃO chama Flush: carregar um save não pode pagar recompensa. Um
            // objetivo que já estava cumprido e não pago no momento do save é
            // pago no primeiro Flush do frame seguinte, uma vez só, como
            // qualquer outro.
            _dirty = true;
            ProgressChanged?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _active.Count; i++) _active[i].Unsubscribe();
            for (int i = 0; i < _busSubscriptions.Count; i++) _busSubscriptions[i]?.Dispose();

            _busSubscriptions.Clear();
        }
    }
}
