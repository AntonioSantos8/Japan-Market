using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
public class GlobalPrices : MonoBehaviour
{

    [SerializeField] PriceDisplayUI _priceDisplay;
  
  Dictionary<Items, float> _globalItemsPrice = new Dictionary<Items, float>();
  Dictionary<Items, bool> _hasPutItem = new Dictionary<Items, bool>();

    [SerializeField] AllIThingsData[] _allItemsData;
    [SerializeField] NumbersDisplay _keyboarNumbers;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        ServiceLocator.Register(this);
    }
    void Start(){foreach (Items item in System.Enum.GetValues(typeof(Items)))
{
    _globalItemsPrice.Add(item, 0);
}
    foreach (Items item in System.Enum.GetValues(typeof(Items)))
{
    _hasPutItem.Add(item, false);
}


}
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
       
       if (_globalItemsPrice.ContainsKey(item))
        _globalItemsPrice[item] = to;

    }
    public void Apply()
    {
        if (currentDisplayType == Items.None)
            return;

        SetCurrentPrice(_keyboarNumbers.Apply(currentDisplayType) , currentDisplayType);

        TutorialManager tutorialManager = ServiceLocator.Get<TutorialManager>();
        if (tutorialManager != null)
            tutorialManager.NotifyGameEvent("HasPutPrice");

       SetCurrentDisplayNone();
    }
    Items currentDisplayType = Items.None;
public void HasPutItem(Items item)
    {
        foreach(var pair in _hasPutItem)
        {
            if(pair.Key == item)
            {
                if(!_hasPutItem[item])
                {
                    _hasPutItem[item] = true;
                     SetCurrentPrice(GetMarketPrice(item) , item);
                _priceDisplay.ShowDisplay(GetItemCurrentPrice(item),GetMarketPrice(item));
                SetCurrentDisplay(item);
               

               

                }
                return;
            }
        }
    
         
    }
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
 