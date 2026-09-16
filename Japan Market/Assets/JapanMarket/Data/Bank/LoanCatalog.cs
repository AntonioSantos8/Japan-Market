using System.Collections.Generic;
using UnityEngine;

namespace JapanMarket.Data
{
    /// <summary>A fonte única de verdade sobre que faixas de empréstimo existem.</summary>
    public interface ILoanCatalog
    {
        IReadOnlyList<LoanDefinition> All { get; }

        /// <summary>Acha a faixa pela chave do save. Null quando sumiu do projeto.</summary>
        LoanDefinition Find(string key);

        /// <summary>Aloca uma lista nova — não chame por frame.</summary>
        List<LoanDefinition> UnlockedFor(IUnlockContext context);
    }

    /// <summary>
    /// Catálogo de faixas de empréstimo. UM asset no projeto, populado pelo import.
    ///
    /// Duas necessidades concretas, não uma: o app Banco precisa listar as faixas
    /// disponíveis, e o save precisa reencontrar a faixa de um contrato em aberto
    /// a partir da chave. Sem o catálogo, a primeira vira um array arrastado à
    /// mão (que diverge) e a segunda vira uma varredura de todos os assets do
    /// projeto em runtime (que não existe no build).
    /// </summary>
    [CreateAssetMenu(fileName = "LoanCatalog",
        menuName = "Japan Market/Loan Catalog", order = 5)]
    public sealed class LoanCatalog : ScriptableObject,
        ILoanCatalog, IEditableCatalog<LoanDefinition>
    {
        [SerializeField] private List<LoanDefinition> _loans = new();

        private Dictionary<string, LoanDefinition> _byKey;

        public IReadOnlyList<LoanDefinition> All => _loans;
        public string CatalogName => "Empréstimos";
        public int EntryCount => _loans.Count;

        private void OnEnable() => Rebuild();

        public LoanDefinition Find(string key)
        {
            EnsureBuilt();

            return string.IsNullOrWhiteSpace(key)
                ? null
                : _byKey.TryGetValue(key.Trim(), out LoanDefinition loan) ? loan : null;
        }

        public List<LoanDefinition> UnlockedFor(IUnlockContext context)
        {
            var result = new List<LoanDefinition>();

            for (int i = 0; i < _loans.Count; i++)
            {
                LoanDefinition loan = _loans[i];
                if (loan == null || !loan.IsValid) continue;
                if (!loan.IsUnlocked(context)) continue;

                result.Add(loan);
            }

            return result;
        }

        public void Rebuild()
        {
            _byKey = new Dictionary<string, LoanDefinition>();

            for (int i = 0; i < _loans.Count; i++)
            {
                LoanDefinition loan = _loans[i];
                if (loan == null) continue;

                if (!_byKey.ContainsKey(loan.Key)) _byKey[loan.Key] = loan;
            }
        }

        private void EnsureBuilt()
        {
            if (_byKey == null) Rebuild();
        }

        public List<CatalogProblem> Validate()
        {
            var problems = new List<CatalogProblem>();
            var keys = new Dictionary<string, LoanDefinition>();

            for (int i = 0; i < _loans.Count; i++)
            {
                LoanDefinition loan = _loans[i];

                if (loan == null)
                {
                    problems.Add(new CatalogProblem(null, "Entrada vazia no catálogo."));
                    continue;
                }

                if (!loan.IsValid)
                {
                    problems.Add(new CatalogProblem(loan,
                        "Contrato inválido: prazo, parcela ou principal não positivo. " +
                        "O banco recusa a contratação."));
                }

                if (!loan.HasKey)
                {
                    problems.Add(new CatalogProblem(loan,
                        "Sem chave: o save vai usar o nome do asset, e renomear perde " +
                        "os contratos em aberto do jogador."));
                }

                if (keys.TryGetValue(loan.Key, out LoanDefinition twin))
                {
                    if (twin != loan)
                        problems.Add(new CatalogProblem(loan,
                            $"Chave '{loan.Key}' repetida com '{twin.name}'. O save não " +
                            "conseguiria distinguir as duas faixas."));

                    continue;
                }

                keys[loan.Key] = loan;
            }

            return problems;
        }

#if UNITY_EDITOR
        public bool EditorSetItems(List<LoanDefinition> items)
        {
            if (_loans.Count == items.Count)
            {
                bool same = true;
                for (int i = 0; i < items.Count; i++)
                    if (_loans[i] != items[i]) { same = false; break; }
                if (same) return false;
            }

            _loans = items;
            Rebuild();
            return true;
        }
#endif
    }
}
