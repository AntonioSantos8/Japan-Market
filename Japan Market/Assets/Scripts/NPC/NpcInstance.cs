using Unity.VisualScripting;
using UnityEngine;

public enum PaymentType { Cash, Card }

/// <summary>
/// Núcleo do NPC: armazena dados de identidade e tipo de pagamento.
/// </summary>
public class NpcInstance : MonoBehaviour
{
    [Header("Data")]
    public NpcData data;

    [Header("Payment")]
    public PaymentType paymentType;

    private void Awake()
    {
        paymentType = (UnityEngine.Random.value > 0.5f) ? PaymentType.Card : PaymentType.Cash;
    }
    void Start()
    {

    }

    private bool _isRegisteredClient = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Entrance") && !_isRegisteredClient)
        {
            _isRegisteredClient = true;
            ServiceLocator.Get<MarketManager>().RegisterClient(transform);
            ServiceLocator.Get<MarketManager>().Clients++;
            
            Debug.Log($"[Market] Cliente entrou. Total: {ServiceLocator.Get<MarketManager>().Clients}");
        }
    }
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Entrance"))
            UnregisterAsClient();
    }

    private void OnDestroy() => UnregisterAsClient();

    private void UnregisterAsClient()
    {
        if (!_isRegisteredClient) return;
        _isRegisteredClient = false;

        MarketManager market = ServiceLocator.Get<MarketManager>();
        if (market == null) return;

        market.UnregisterClient(transform);
        market.Clients--;
        Debug.Log($"[Market] Cliente saiu. Total: {market.Clients}");
    }
}