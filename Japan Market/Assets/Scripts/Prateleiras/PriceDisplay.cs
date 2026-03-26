using UnityEngine;

public class PriceDisplay : InteractableBase
{

    [SerializeField] Segment _segment;
    public Items GetDisplayItemType => _segment.SegmenyType;
    public override void Interact()
    {
          if(_segment.SegmenyType.Equals(Items.None)) return;
         ServiceLocator.Get<GlobalPrices>().EnablePriceDisplayUI(this);
        
    }
    public override void OnLookAt()
    {
        if(_segment.SegmenyType.Equals(Items.None)) return;
        


    }   
  
  
    



}
