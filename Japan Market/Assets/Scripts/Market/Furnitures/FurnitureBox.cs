using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class FurnitureBox : InteractableBase
{
    [SerializeField] private FurnitureType type;
    [SerializeField] private FurnitureData data;
    [SerializeField] private Image furnitureImage;
    // FurnitureBox.cs
    public FurnitureData GetData() => data;
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
        if (ServiceLocator.Get<FurnitureManager>().HasFurnitureInInventory)
        {
            ServiceLocator.Get<Warnings>().ShowWarning("Your inventory is full.", false);
            yield break;
        }
        ServiceLocator.Get<FurnitureManager>().HasFurnitureInInventory = true;
        ServiceLocator.Get<FurnitureManager>().ToggleBuildingMode();
        yield return new WaitForSeconds(0.1f);
        ServiceLocator.Get<FurnitureManager>().SelectFurniture(type);
        Destroy(this.gameObject);
    }
}
