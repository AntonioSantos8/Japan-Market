using DG.Tweening;
using TMPro;
using UnityEngine;
[System.Serializable]
public class PcItems
{
    [SerializeField] Transform visual;
    [SerializeField] AllIThingsData data;

    public GameObject Obj => visual.gameObject;
    public Transform Visual { get => visual; set => visual = value; }
    public AllIThingsData Data { get => data; set => data = value; }
}
public class ShopBuyItems : MonoBehaviour
{
    [SerializeField] PcItems[] objects;
    [SerializeField] float duration = 0.25f;

    int currentIndex = 0;
    Vector3[] originalScales;
    bool isTweening;
    GameObject currentItemPrefab, currentItemBox;
    [SerializeField]
    TMP_Text nameText, descriptionText,
     singlePriceText;
    [SerializeField] Transform boxesSpawnPoint;
    void Start()
    {
        originalScales = new Vector3[objects.Length];

        for (int i = 0; i < objects.Length; i++)
        {
            originalScales[i] = objects[i].Visual.localScale;

            if (i == currentIndex)
                objects[i].Obj.SetActive(true);
            else
            {
                objects[i].Obj.SetActive(false);
                Vector3 s = objects[i].Visual.localScale;
                s.y = 0;
                objects[i].Visual.localScale = s;
            }
        }
        if (objects[currentIndex].Obj.TryGetComponent(out ItemsExample atd))
        {
            AllIThingsData at = atd.GetAllThingsData();
            currentItemPrefab = at.itemPrefab;
            currentItemBox = at.itemBoxPrefab;
            nameText.text = at.name;
            descriptionText.text = at.description;
            singlePriceText.text = "¥" + Mathf.RoundToInt(at.singleItemPrice);
        }
    }
    public void BuyBox()
    {
        float price = 0f;

        if (objects[currentIndex].Obj.TryGetComponent(out ItemsExample atd))
        {
            AllIThingsData at = atd.GetAllThingsData();
            price = at.singleItemPrice;
        }
        else if (objects[currentIndex].Obj.TryGetComponent(out FurnitureBox fb))
        {
            price = fb.GetData().data.singleItemPrice;
        }

        MarketManager market = ServiceLocator.Get<MarketManager>();

        if (market.Money < price)
        {
            ServiceLocator.Get<Warnings>().ShowWarning("Not enough money.", false);
            return;
        }

        market.Lose_Money(price);
        Instantiate(currentItemBox, boxesSpawnPoint.position, Quaternion.identity);
    }

    void Update()
    {
       
        if (isTweening) return;

        if (Input.GetKeyDown(KeyCode.Q)) Previous();
        if (Input.GetKeyDown(KeyCode.E)) Next();

    }

    public void Next()
    {
         if(objects.Length == 1) return;
        ChangeItem(currentIndex + 1 >= objects.Length ? 0 : currentIndex + 1);
    }

    public void Previous()
    {
         if(objects.Length == 1) return;
        ChangeItem(currentIndex - 1 < 0 ? objects.Length - 1 : currentIndex - 1);

    }

    void ChangeItem(int newIndex)
    {
        if (isTweening) return;

        isTweening = true;

        Transform current = objects[currentIndex].Visual;
        Transform next = objects[newIndex].Visual;


        DOTween.Kill(nameText.transform);
        DOTween.Kill(descriptionText.transform);
        DOTween.Kill(singlePriceText.transform);
        //DOTween.Kill(buySingleButton);
        //DOTween.Kill(buyBoxButton);


        Sequence uiClose = DOTween.Sequence();

        uiClose.Append(nameText.transform.DOScaleX(0f, duration));
        uiClose.Join(descriptionText.transform.DOScaleX(0f, duration));
        uiClose.Join(singlePriceText.transform.DOScaleX(0f, duration));
        //uiClose.Join(buySingleButton.DOScaleX(0f, duration));
        //uiClose.Join(buyBoxButton.DOScaleX(0f, duration));

        uiClose.OnComplete(() =>
        {

            current.DOScaleY(0f, duration).OnComplete(() =>
            {
                objects[currentIndex].Obj.SetActive(false);
                currentIndex = newIndex;
                objects[currentIndex].Obj.SetActive(true);

                Vector3 startScale = originalScales[currentIndex];
                startScale.y = 0;
                next.localScale = startScale;


                if (next.TryGetComponent(out ItemsExample atd))
                {
                    AllIThingsData at = atd.GetAllThingsData();

                    currentItemPrefab = at.itemPrefab;
                    currentItemBox = at.itemBoxPrefab;

                    nameText.text = at.name;
                    descriptionText.text = at.description;
                    singlePriceText.text = "¥" + Mathf.RoundToInt(at.singleItemPrice);
                }


                next.DOScaleY(originalScales[currentIndex].y, duration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {

                        Sequence uiOpen = DOTween.Sequence();

                        uiOpen.Append(nameText.transform.DOScaleX(1f, duration).SetEase(Ease.OutBack));
                        uiOpen.Join(descriptionText.transform.DOScaleX(1f, duration).SetEase(Ease.OutBack));
                        uiOpen.Join(singlePriceText.transform.DOScaleX(1f, duration).SetEase(Ease.OutBack));
                        // uiOpen.Join(buySingleButton.DOScaleX(1f, duration).SetEase(Ease.OutBack));
                        //uiOpen.Join(buyBoxButton.DOScaleX(1f, duration).SetEase(Ease.OutBack));

                        uiOpen.OnComplete(() =>
                        {
                            isTweening = false;
                        });
                    });
            });
        });
    }
    public void RefreshCurrentItem()
    {
        ChangeItem(currentIndex);
    }
}