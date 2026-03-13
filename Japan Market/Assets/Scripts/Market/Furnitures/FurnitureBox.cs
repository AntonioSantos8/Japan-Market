using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class FurnitureBox : InteractableBase
{
    [SerializeField] private FurnitureType type;
    [SerializeField] private FurnitureData data;
    [SerializeField] private Image furnitureImage;
    void Start()
    {
        furnitureImage.sprite = data.furnitureImage;
    }
    public override void Interact()
    {
        StartCoroutine(BuildModeCoroutine());
    }
    IEnumerator BuildModeCoroutine()
    {
        ServiceLocator.Get<FurnitureManager>().hasFurnitureInInventory = true;
        ServiceLocator.Get<FurnitureManager>().ToggleBuildingMode();
        yield return new WaitForSeconds(0.1f);
        ServiceLocator.Get<FurnitureManager>().SelectFurniture(type);
        Destroy(this.gameObject);
    }   
}
