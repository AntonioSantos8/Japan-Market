using UnityEngine;
using DG.Tweening;
using TMPro;

public class MarketManager : MonoBehaviour
{
    private bool open = false;
    private float money;
    private float late_money;
    private Tweener moneyTween;
    [SerializeField] private float clients;
    [SerializeField] private TextMeshProUGUI moneyText;

    public float Money { get => money; set => money = value; }
    public bool Open { get => open; set => open = value; }
    public float Clients { get => clients; set => clients = value; }

    void Start()
    {
        LoadMoney();
        ServiceLocator.Register(this);
    }

    public void LoadMoney()
    {
        late_money = money;
        moneyText.text = $"Money: {FormatMoney(money)}";
    }

    [ContextMenu("Test Earn")]
    public void TestEarn() => Earn_Money(1000);

    public void Earn_Money(float quantity)
    {
        money += quantity;

        moneyTween?.Kill();
        moneyTween = DOTween.To(
            () => late_money,
            x =>
            {
                late_money = x;
                moneyText.text = $"Money: {FormatMoney(late_money)}";
            },
            money,
            1f
        ).SetEase(Ease.OutQuad);
    }

    public void Lose_Money(float quantity)
    {
        if (money >= quantity)
        {
            money -= quantity;

            moneyTween?.Kill();
            moneyTween = DOTween.To(
                () => late_money,
                x =>
                {
                    late_money = x;
                    moneyText.text = $"Money: {FormatMoney(late_money)}";
                },
                money,
                1f
            ).SetEase(Ease.OutQuad);
        }
    }
    private string FormatMoney(float value)
    {
        if (value >= 1_000_000_000f) return $"{value / 1_000_000_000f:0.##}b";
        if (value >= 1_000_000f) return $"{value / 1_000_000f:0.##}m";
        if (value >= 1_000f) return $"{value / 1_000f:0.##}k";
        return Mathf.FloorToInt(value).ToString();
    }
}