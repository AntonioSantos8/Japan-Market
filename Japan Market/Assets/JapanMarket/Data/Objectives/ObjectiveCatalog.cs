using System.Collections.Generic;
using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>A fonte única de verdade sobre quais objetivos existem.</summary>
    public interface IObjectiveCatalog
    {
        IReadOnlyList<ObjectiveDefinition> All { get; }

        bool TryGet(ObjectiveId id, out ObjectiveDefinition definition);
    }

    /// <summary>
    /// Catálogo de objetivos. UM asset no projeto inteiro.
    ///
    /// Mesma mecânica do <see cref="ItemCatalog"/>: a lista não é arrastada, um
    /// AssetPostprocessor a reconstrói quando um ObjectiveDefinition é criado,
    /// movido ou apagado. Cadastrar um objetivo é criar um asset, e nada mais.
    ///
    /// A busca por id existe por causa do save: ele guarda "objetivo X está em 3
    /// de 10", e no carregamento é preciso reencontrar o asset a partir do id.
    /// </summary>
    [CreateAssetMenu(fileName = "ObjectiveCatalog",
        menuName = "Japan Market/Objective Catalog", order = 4)]
    public sealed class ObjectiveCatalog : ScriptableObject,
        IObjectiveCatalog, IEditableCatalog<ObjectiveDefinition>
    {
        [SerializeField] private List<ObjectiveDefinition> _objectives = new();

        private Dictionary<ObjectiveId, ObjectiveDefinition> _byId;

        public IReadOnlyList<ObjectiveDefinition> All => _objectives;
        public string CatalogName => "Objetivos";
        public int EntryCount => _objectives.Count;

        private void OnEnable() => Rebuild();

        public bool TryGet(ObjectiveId id, out ObjectiveDefinition definition)
        {
            EnsureBuilt();
            return _byId.TryGetValue(id, out definition);
        }

        public void Rebuild()
        {
            _byId = new Dictionary<ObjectiveId, ObjectiveDefinition>();

            for (int i = 0; i < _objectives.Count; i++)
            {
                ObjectiveDefinition objective = _objectives[i];
                if (objective == null || !objective.Id.IsValid) continue;

                // Primeiro a entrar vence, e o duplicado é reportado pelo
                // Validate. Sobrescrever em silêncio faria dois objetivos
                // dividirem um progresso só no save.
                if (_byId.ContainsKey(objective.Id)) continue;

                _byId[objective.Id] = objective;
            }
        }

        private void EnsureBuilt()
        {
            if (_byId == null) Rebuild();
        }

        public List<CatalogProblem> Validate()
        {
            var problems = new List<CatalogProblem>();
            var seen = new Dictionary<ObjectiveId, ObjectiveDefinition>();

            for (int i = 0; i < _objectives.Count; i++)
            {
                ObjectiveDefinition objective = _objectives[i];

                if (objective == null)
                {
                    problems.Add(new CatalogProblem(null, "Entrada vazia no catálogo."));
                    continue;
                }

                string problem = objective.DescribeProblem();
                if (problem != null) problems.Add(new CatalogProblem(objective, problem));

                if (objective.Title.IsEmpty)
                    problems.Add(new CatalogProblem(objective, "Sem título — a UI não tem o que mostrar."));

                if (objective.Reward.IsEmpty)
                    problems.Add(new CatalogProblem(objective,
                        "Recompensa vazia: concluir não dá dinheiro, XP nem flag."));

                if (!objective.Id.IsValid) continue;

                if (seen.TryGetValue(objective.Id, out ObjectiveDefinition other))
                {
                    problems.Add(new CatalogProblem(objective,
                        $"Id repetido com '{other.name}'. Os dois dividiriam o mesmo " +
                        "progresso no save — duplicar um asset copia o id junto."));
                    continue;
                }

                seen[objective.Id] = objective;
            }

            return problems;
        }

#if UNITY_EDITOR
        /// <summary>Chamado pelo AssetPostprocessor. Devolve true se mudou algo.</summary>
        public bool EditorSetItems(List<ObjectiveDefinition> items)
        {
            if (_objectives.Count == items.Count)
            {
                bool same = true;
                for (int i = 0; i < items.Count; i++)
                    if (_objectives[i] != items[i]) { same = false; break; }
                if (same) return false;
            }

            _objectives = items;
            Rebuild();
            return true;
        }
#endif
    }
}
