using UnityEngine;
using TMPro;
public class PaymentMoney : MonoBehaviour
{
    [SerializeField] GameObject imagePayment;
    [SerializeField] TextMeshProUGUI numberText;
    private void Start()
    {
        imagePayment.SetActive(false);
    }
    void OnMouseDown()
    {
        OpenPayment();
    }
    public void OpenPayment()
    {
        imagePayment.SetActive(true);
    }
    public void AddNumber(string number)
    {

    }
    public void ClearAll()
    {

    }
    public void Undo()
    {

    }

}
