using DG.Tweening;
using TMPro;
using UnityEngine;
public enum SellingItemType{ Furniture, Food}
[System.Serializable]
public class PcItems
{
    [SerializeField] Transform visual;
    [SerializeField] AllIThingsData data;

    public GameObject Obj => visual.gameObject;
    public Transform Visual { get => visual; set => visual = value; }
    public AllIThingsData Data { get => data; set => data = value; }
}
public class ComputerStats
{
    string name;
    string description;
    float singlePrice;

    public string Name { get => name; set => name = value; }
    public string Description { get => description; set => description = value; }
    public float SinglePrice { get => singlePrice; set => singlePrice = value; }
}

public class ShopBuyItems : MonoBehaviour
{
    [SerializeField] PcItems[] objects;
    [SerializeField] float duration = 0.25f;
    PcItems currentObj;
    int currentIndex = 0;
    Vector3[] originalScales;
    bool isTweening;
    GameObject currentItemPrefab, currentItemBox;
    [SerializeField]
    TMP_Text nameText, descriptionText,
     singlePriceText;
    [SerializeField] Transform boxesSpawnPoint;
    [SerializeField] SellingItemType _sellingItemType;
    TutorialManager _tutorialManager;
    void Awake(){ _tutorialManager = ServiceLocator.Get<TutorialManager>();}
    void Start()
    {
        originalScales = new Vector3[objects.Length];

        for (int i = 0; i < objects.Length; i++)
        {
            originalScales[i] = objects[i].Visual.localScale;

            if (i == currentIndex)
            {   
                currentObj = objects[i];
                objects[i].Obj.SetActive(true);
                
            }
            else
            {
                objects[i].Obj.SetActive(false);
                Vector3 s = objects[i].Visual.localScale;
                s.y = 0;
                objects[i].Visual.localScale = s;
            }
        }


        AllIThingsData at = currentObj.Data;
        currentItemPrefab = at.itemPrefab;
        currentItemBox = at.itemBoxPrefab;
        nameText.text = at.name;
        descriptionText.text = at.description;
        singlePriceText.text = "¥" + Mathf.RoundToInt(at.singleItemPrice);
        
    }
    public void BuyBox()
    {
        float price = 0f;

        price = currentObj.Data.singleItemPrice;

        MarketManager market = ServiceLocator.Get<MarketManager>();

        if (market.Money < price)
        {
            ServiceLocator.Get<SoundManager>().Play(SFX.NaoPodePagarSemDinheiro);
            ServiceLocator.Get<Warnings>().ShowWarning("Not enough money.", false);
            return;
        }

        market.Lose_Money(price);
        ServiceLocator.Get<SoundManager>().Play(SFX.ComprarItemOuFurnitureComputador);
        if(_tutorialManager)
            _tutorialManager.BoughtItem(_sellingItemType);
        GameObject spawnedBox = Instantiate(currentItemBox, boxesSpawnPoint.position, Quaternion.identity);
        var itemBox = spawnedBox.GetComponentInChildren<ItemBox>();
        if (itemBox != null)
        {
            itemBox.InitializeBox(currentObj.Data.itemType);
        }
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
        ServiceLocator.Get<SoundManager>().Play(SFX.NavegacaoBotoesComputador);
        ChangeItem(currentIndex + 1 >= objects.Length ? 0 : currentIndex + 1);
    }

    public void Previous()
    {
         if(objects.Length == 1) return;
        ServiceLocator.Get<SoundManager>().Play(SFX.NavegacaoBotoesComputador);
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

                currentObj = objects[currentIndex];

                Vector3 startScale = originalScales[currentIndex];
                startScale.y = 0;
                next.localScale = startScale;



                     AllIThingsData at = currentObj.Data;

                    currentItemPrefab = at.itemPrefab;
                    currentItemBox = at.itemBoxPrefab;
                  
                    
                    nameText.text = at.name;
                    descriptionText.text = at.description;
                    singlePriceText.text = "¥" + Mathf.RoundToInt(at.singleItemPrice);
                


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
    public void UpdateComputerTexts(ComputerStats stats) 
    {
        nameText.text = stats.Name;
        descriptionText.text = stats.Description;
        singlePriceText.text = " " + Mathf.RoundToInt(stats.SinglePrice);
    }
}