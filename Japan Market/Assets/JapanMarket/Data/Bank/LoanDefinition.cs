using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{

    [CreateAssetMenu(fileName = "Loan", menuName = "Japan Market/Loan Tier", order = 3)]
    public sealed class LoanDefinition : ScriptableObject
    {
        [Header("Empréstimo")]
        [Tooltip("Identificador estável, minúsculo e sem espaços: inicial, medio, alto. " +
                 "É o que o save guarda — mudar isto perde os contratos em aberto.")]
        [SerializeField] private string _key;

        [SerializeField] private LocalizedText _displayName;

        [Tooltip("Valor depositado na conta do jogador ao contratar.")]
        [SerializeField] private Money _principal;

        [Tooltip("Parcela fixa cobrada no fechamento de cada dia.")]
        [SerializeField] private Money _dailyPayment;

        [Min(1)]
        [Tooltip("Quantos dias o jogador paga até quitar. Mínimo 1: com zero, o " +
                 "empréstimo seria depositado e quitado antes da primeira cobrança.")]
        [SerializeField] private int _termDays = 10;

        [Header("Progressão")]
        [Tooltip("Condição para esta faixa liberar (ex.: Nível da Loja 10).")]
        [SerializeField] private UnlockCondition _unlock;

        /// <summary>
        /// Cai para o nome do asset quando vazio: o save nunca fica sem chave, e
        /// o validador é quem reclama de quem não preencheu.
        /// </summary>
        public string Key => string.IsNullOrWhiteSpace(_key) ? name : _key.Trim();
        public bool HasKey => !string.IsNullOrWhiteSpace(_key);

        public LocalizedText DisplayName => _displayName;
        public Money Principal => _principal;
        public Money DailyPayment => _dailyPayment;
        public int TermDays => _termDays;
        public UnlockCondition Unlock => _unlock;

        public Money TotalCost => _dailyPayment * _termDays;

        public bool IsUnlocked(IUnlockContext context) =>
            _unlock == null || _unlock.IsSatisfied(context);

        /// <summary>
        /// Contrato utilizável? Prazo zero quitaria antes de cobrar; parcela
        /// zero nunca é cobrada (o ExpenseService filtra valor não positivo) e
        /// daria um empréstimo de graça que se auto-quita.
        ///
        /// E principal não positivo é o contrário do defeito, mas igualmente
        /// impossível: o jogador contrata, não recebe nada (o livro-razão recusa
        /// depósito não positivo) e passa dez dias pagando parcela. Um asset mal
        /// preenchido não pode virar uma dívida sem empréstimo.
        /// </summary>
        public bool IsValid =>
            _termDays >= 1 && _dailyPayment.IsPositive && _principal.IsPositive;
    }
}

