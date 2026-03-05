using UnityEngine;
[CreateAssetMenu(fileName = "All Things", menuName = "Scriptable Objects/Create Thing")]
public class AllIThingsData : ScriptableObject
{
   public  string itemName, description;
  public   float singleItemPrice, boxPrice;
   public  Items itemType;
    public GameObject itemPrefab;
    public GameObject itemBoxPrefab;

}
