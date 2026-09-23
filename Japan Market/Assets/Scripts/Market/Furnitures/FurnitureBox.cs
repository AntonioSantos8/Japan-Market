using UnityEngine;
using UnityEngine.UI;

public class FurnitureBox : InteractableBase
{
    [SerializeField] private FurnitureData data;
    [SerializeField] private Image furnitureImage;

    public FurnitureData GetData() => data;

    public void Initialize(FurnitureData furniture)
    {
        data = furniture;
        RefreshImage();
    }

    private void Start()
    {
        RefreshImage();
    }

    private void RefreshImage()
    {
        if (furnitureImage == null) return;
        furnitureImage.sprite = data != null ? data.furnitureImage : null;
        furnitureImage.enabled = furnitureImage.sprite != null;
    }

    public override void Interact()
    {
        FurnitureManager manager = ServiceLocator.Get<FurnitureManager>();
        if (manager == null || data == null) return;

        manager.AddToInventory(data);
        Destroy(gameObject);
    }
}
