using System.Collections;
using UnityEngine;
public class FurnitureBox : InteractableBase
{
    [SerializeField] private FurnitureType type;
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
