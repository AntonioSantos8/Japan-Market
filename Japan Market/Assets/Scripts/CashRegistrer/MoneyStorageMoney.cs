using UnityEngine;
using DG.Tweening;
public enum MoneyType
{
    Coin,
    Bill
}
public class MoneyStorageMoney : InteractableBase
{
    [SerializeField] MoneyType moneyType;
    [SerializeField] MoneyInstance moneyPrefab;
    [SerializeField] float value;
    public override void Interact()
    {
        CashRegister cashRegister = ServiceLocator.Get<CashRegister>();
        if (cashRegister == null || moneyPrefab == null) return;

        if (cashRegister.GetCurrentPaymentType() != PaymentType.Cash)
        {
            Debug.LogWarning($"[MoneyStorageMoney] Bloqueado: cliente paga com {cashRegister.GetCurrentPaymentType()}, não com dinheiro.");
            return;
        }

        // A nota só é criada se fizer parte de um pagamento em dinheiro aberto.
        if (!cashRegister.AddMoney(value)) return;
        print("devia spawna");
        MoneyInstance money = Instantiate(moneyPrefab, transform.position, Quaternion.identity);
        money.Setup(cashRegister, value);
        cashRegister.RegisterMoneyInstance(money);
        money.GoToPosition(cashRegister.GetMoneyPosition(moneyType));
    }

    

}
