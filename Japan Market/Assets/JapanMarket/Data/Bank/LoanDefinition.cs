using JapanMarket.Core;
using UnityEngine;

namespace JapanMarket.Data
{

    [CreateAssetMenu(fileName = "Loan", menuName = "Japan Market/Loan Tier", order = 3)]
    public sealed class LoanDefinition : ScriptableObject
    {
        [Header("Loan")]
        [SerializeField] private LocalizedText _displayName;

        [Tooltip("Total value deposited in the player's account upon taking the loan.")]
        [SerializeField] private Money _principal;

        [Tooltip("Fixed amount deducted from the balance at the end of each day.")]
        [SerializeField] private Money _dailyPayment;

        [Tooltip("How many days the player needs to pay to clear the loan.")]
        [SerializeField] private int _termDays = 10;

        [Header("Progression")]
        [Tooltip("Condition for this tier to be unlocked (e.g., Store Level 10).")]
        [SerializeField] private UnlockCondition _unlock;

        public LocalizedText DisplayName => _displayName;
        public Money Principal => _principal;
        public Money DailyPayment => _dailyPayment;
        public int TermDays => _termDays;
        public UnlockCondition Unlock => _unlock;

        public Money TotalCost => _dailyPayment * _termDays;

        public bool IsUnlocked(IUnlockContext context) =>
            _unlock == null || _unlock.IsSatisfied(context);
    }
}

