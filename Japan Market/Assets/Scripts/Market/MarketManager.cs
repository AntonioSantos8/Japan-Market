using UnityEngine;
using DG.Tweening;
using TMPro;

public class MarketManager : MonoBehaviour 
{
    private bool open = false;
    private float cash;
    private float late_cash;
    private Tweener cashTween;

    [SerializeField] private TextMeshProUGUI cashText;

    public float Cash { get => cash; set => cash = value; }

    void Start()
    {
        ServiceLocator.Register(this);
    }

    public void Earn_Cash(float quantity)
    {
        cash += quantity;

        cashTween?.Kill();
        cashTween = DOTween.To(
            () => late_cash,
            x => {
                late_cash = x;
                cashText.text = Mathf.FloorToInt(late_cash).ToString();
            },
            cash,
            1f
        ).SetEase(Ease.OutQuad);
    }

    public void Lose_Cash(float quantity)
    {
        if (cash >= quantity)
            cash -= quantity;
    }
}