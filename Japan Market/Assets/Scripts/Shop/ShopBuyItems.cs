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

    [SerializeField] TMP_Text nameText, descriptionText, priceText;
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
            nameText.text = at.name;
           descriptionText.text = at.description;
           priceText.text = at.itemPrice.ToString();
        }
    }

    void Update()
    {
        if (isTweening) return;

        if (Input.GetKeyDown(KeyCode.Q)) Previous();
        if (Input.GetKeyDown(KeyCode.E)) Next();
        if (Input.GetKeyDown(KeyCode.RightArrow)) Next();
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Previous();
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

        current.DOScaleY(0f, duration).OnComplete(() =>
        {
            objects[currentIndex].SetActive(false);

            currentIndex = newIndex;

            objects[currentIndex].SetActive(true);

            Vector3 startScale = originalScales[currentIndex];
            startScale.y = 0;
            next.localScale = startScale;
           if( next.gameObject.TryGetComponent(out ItemsExample atd))
            {
                AllIThingsData at = atd.GetAllThingsData();
                nameText.transform.DOScaleX(0f, duration);
                descriptionText.transform.DOScaleX(0f, duration);
                priceText.transform.DOScaleX(0f, duration).OnComplete(() => {
                    nameText.text = at.name;
                    descriptionText.text = at.description;
                    priceText.text = at.itemPrice.ToString();
                    nameText.transform.DOScaleX(1f, duration).SetEase(Ease.OutBack);
                    descriptionText.transform.DOScaleX(1f, duration).SetEase(Ease.OutBack);
                    priceText.transform.DOScaleX(1f, duration).SetEase(Ease.OutBack);
                });
               

                
            }
            next.DOScaleY(originalScales[currentIndex].y, duration).SetEase(Ease.OutBack).OnComplete(() =>
            {
                isTweening = false;
            });
        });
    }
}