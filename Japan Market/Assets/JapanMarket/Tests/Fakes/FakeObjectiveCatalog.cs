using System;
using System.Collections.Generic;
using JapanMarket.Core;
using JapanMarket.Data;

namespace JapanMarket.Tests
{
    /// <summary>
    /// Catálogo de objetivos de mentira, com a lista que o teste mandar.
    /// O de verdade é um ScriptableObject populado pelo import.
    /// </summary>
    public sealed class FakeObjectiveCatalog : IObjectiveCatalog
    {
        private readonly List<ObjectiveDefinition> _all = new();

        public FakeObjectiveCatalog(params ObjectiveDefinition[] objectives)
        {
            if (objectives != null) _all.AddRange(objectives);
        }

        public IReadOnlyList<ObjectiveDefinition> All => _all;

        public void Add(ObjectiveDefinition objective) => _all.Add(objective);

        /// <summary>Simula o asset apagado do projeto entre duas versões do jogo.</summary>
        public void Remove(ObjectiveDefinition objective) => _all.Remove(objective);

        public bool TryGet(ObjectiveId id, out ObjectiveDefinition definition)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i] == null || _all[i].Id != id) continue;

                definition = _all[i];
                return true;
            }

            definition = null;
            return false;
        }
    }

    /// <summary>
    /// Uma condição que ouve TRANSAÇÕES — inclusive a que paga a recompensa.
    ///
    /// Existe só nos testes, e existe para provar o pior caso do projeto: o
    /// EventBus tem guarda de recursão POR TIPO e lança. Um objetivo que ouve
    /// TransactionRecorded, com recompensa em dinheiro, é exatamente a
    /// combinação que estouraria se o serviço pagasse dentro do despacho em vez
    /// de no Flush. Nenhuma condição de produção faz isso hoje — e é justamente
    /// por isso que precisa existir uma aqui: a armadilha só é um bug no dia em
    /// que alguém escrever a primeira.
    /// </summary>
    public sealed class WatchTransactionsCondition : ObjectiveCondition
    {
        public override IDisposable Watch(IEventBus events, IObjectiveProgress progress) =>
            events?.Subscribe<TransactionRecorded>(_ => progress.Add(1));

        public override string Describe() => $"Registrar {Target} transação(ões)";
    }
}
