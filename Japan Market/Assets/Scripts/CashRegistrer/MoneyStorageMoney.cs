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
        
            Instantiate(moneyPrefab, transform.position, Quaternion.identity).GoToPosition(ServiceLocator.Get<CashRegister>().GetMoneyPosition(moneyType));
        
    }

    

}
