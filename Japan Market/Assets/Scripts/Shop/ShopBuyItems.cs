using DG.Tweening;
using TMPro;
using UnityEngine;

public class ShopBuyItems : MonoBehaviour
{
    [SerializeField] GameObject[] objects;
    [SerializeField] float duration = 0.25f;

    int currentIndex = 0;
    Vector3[] originalScales;
    bool isTweening;
    GameObject currentItemPrefab, currentItemBox;
    [SerializeField] TMP_Text nameText, descriptionText, singlePriceText, boxPriceText;
    void Start()
    {
        originalScales = new Vector3[objects.Length];

        for (int i = 0; i < objects.Length; i++)
        {
            originalScales[i] = objects[i].transform.localScale;

            if (i == currentIndex)
                objects[i].SetActive(true);
            else
            {
                objects[i].SetActive(false);
                Vector3 s = objects[i].transform.localScale;
                s.y = 0;
                objects[i].transform.localScale = s;
            }
        }
        if (objects[currentIndex].gameObject.TryGetComponent(out ItemsExample atd))
        {
            AllIThingsData at = atd.GetAllThingsData();
            currentItemPrefab = at.itemPrefab;
            currentItemBox = at.itemBoxPrefab;
            nameText.text = at.name;
           descriptionText.text = at.description;
           singlePriceText.text = at.singleItemPrice.ToString();
            if(boxPriceText != null)
            boxPriceText.text = at.boxPrice.ToString();
        }
    }
  
    public void BuyNormalItem()
    {
        Instantiate(currentItemPrefab, new Vector3(0,0,0), Quaternion.identity);



    }
    public void BuyBox()
    {
        Instantiate(currentItemBox, new Vector3(0, 0, 0), Quaternion.identity);



    }

    void Update()
    {
        if (isTweening) return;

        if (Input.GetKeyDown(KeyCode.Q)) Previous();
        if (Input.GetKeyDown(KeyCode.E)) Next();
   
    }

    public void Next()
    {
        ChangeItem(currentIndex + 1 >= objects.Length ? 0 : currentIndex + 1);
    }

    public void Previous()
    {
        ChangeItem(currentIndex - 1 < 0 ? objects.Length - 1 : currentIndex - 1);
      
    }

    void ChangeItem(int newIndex)
    {
        if (isTweening || newIndex == currentIndex) return;

        isTweening = true;

        Transform current = objects[currentIndex].transform;
        Transform next = objects[newIndex].transform;

       
        DOTween.Kill(nameText.transform);
        DOTween.Kill(descriptionText.transform);
        DOTween.Kill(singlePriceText.transform);
        if (boxPriceText != null)
            DOTween.Kill(boxPriceText.transform);
        //DOTween.Kill(buySingleButton);
        //DOTween.Kill(buyBoxButton);


        Sequence uiClose = DOTween.Sequence();

        uiClose.Append(nameText.transform.DOScaleX(0f, duration));
        uiClose.Join(descriptionText.transform.DOScaleX(0f, duration));
        uiClose.Join(singlePriceText.transform.DOScaleX(0f, duration));
        if (boxPriceText != null)
            uiClose.Join(boxPriceText.transform.DOScaleX(0f, duration));
        //uiClose.Join(buySingleButton.DOScaleX(0f, duration));
        //uiClose.Join(buyBoxButton.DOScaleX(0f, duration));

        uiClose.OnComplete(() =>
        {
           
            current.DOScaleY(0f, duration).OnComplete(() =>
            {
                objects[currentIndex].SetActive(false);
                currentIndex = newIndex;
                objects[currentIndex].SetActive(true);

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
                    singlePriceText.text = at.singleItemPrice.ToString();
                    if (boxPriceText != null)
                        boxPriceText.text = at.boxPrice.ToString();
                }

            
                next.DOScaleY(originalScales[currentIndex].y, duration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {
                     
                        Sequence uiOpen = DOTween.Sequence();

                        uiOpen.Append(nameText.transform.DOScaleX(1f, duration).SetEase(Ease.OutBack));
                        uiOpen.Join(descriptionText.transform.DOScaleX(1f, duration).SetEase(Ease.OutBack));
                        uiOpen.Join(singlePriceText.transform.DOScaleX(1f, duration).SetEase(Ease.OutBack));
                        if (boxPriceText != null)
                            uiOpen.Join(boxPriceText.transform.DOScaleX(1f, duration).SetEase(Ease.OutBack));
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
}