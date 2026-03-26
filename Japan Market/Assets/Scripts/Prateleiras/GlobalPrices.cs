using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
public class GlobalPrices : MonoBehaviour
{

    [SerializeField] PriceDisplayUI _priceDisplay;
  
  Dictionary<Items, float> _globalItemsPrice = new Dictionary<Items, float>();

    [SerializeField] AllIThingsData[] _allItemsData;
    [SerializeField] NumbersDisplay _keyboarNumbers;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        ServiceLocator.Register(this);
    }
    void Start(){foreach (Items item in System.Enum.GetValues(typeof(Items)))
{
    _globalItemsPrice.Add(item, GetMarketPrice(item));
}}
    public float GetItemCurrentPrice(Items item)
    {
        foreach(var pair in _globalItemsPrice)
        {
                if(pair.Key == item)
            {
                return _globalItemsPrice[item];

            }

        }
        print("Não tem o item no dicio");
        return 0;

    }
    void SetCurrentPrice(float to, Items item)
    {
        if(to == 0) return;
       if (_globalItemsPrice.ContainsKey(item))
        _globalItemsPrice[item] = to;

    }
    public void Apply()
    {
        SetCurrentPrice(_keyboarNumbers.Apply() , currentDisplayType);

       SetCurrentDisplayNone();
    }
    Items currentDisplayType = Items.None;

    public  void SetCurrentDisplay(Items to){currentDisplayType=to;}
    public void SetCurrentDisplayNone(){currentDisplayType = Items.None;}
    float GetMarketPrice(Items item)
    {
        
        foreach(var data in _allItemsData)
        {
            if(data.itemType == item){ return data.marketPrice;}


        }
        return 0;

    }
  
    public void EnablePriceDisplayUI(PriceDisplay display)
    {
        _priceDisplay.ShowDisplay(GetItemCurrentPrice(display.GetDisplayItemType),GetMarketPrice(display.GetDisplayItemType));
        SetCurrentDisplay(display.GetDisplayItemType);
    }
}
 