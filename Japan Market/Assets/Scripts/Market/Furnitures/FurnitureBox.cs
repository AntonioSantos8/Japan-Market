using UnityEngine;
using UnityEngine.UI;

public class FurnitureBox : InteractableBase
{
    [SerializeField] private FurnitureData data;
    [SerializeField] private Image furnitureImage;

    public FurnitureData GetData() => data;

    private void Start()
    {
        if (furnitureImage != null && data != null)
            furnitureImage.sprite = data.furnitureImage;
    }

    public override void Interact()
    {
        ServiceLocator.Get<FurnitureManager>().AddToInventory(data);
        Destroy(gameObject);
    }
}
