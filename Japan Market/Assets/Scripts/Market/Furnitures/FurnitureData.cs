using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "Furniture", menuName = "Scriptable Objects/Furniture")]
public class FurnitureData : ScriptableObject
{
    public FurnitureType type;
    public string furnitureName;
    public GameObject prefab;
    public GameObject ghostPrefab;
    [Tooltip("Caixa física entregue quando este móvel é comprado.")]
    public GameObject deliveryBoxPrefab;
    public float floorDistance;
    public Sprite furnitureImage;
    public AllIThingsData data;
}   

public enum FurnitureType
{
    None,
    Shelf,
    Freezer,
    Counter
}
