using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "Furniture", menuName = "Scriptable Objects/Furniture")]
public class FurnitureData : ScriptableObject
{
    public FurnitureType type;
    public string furnitureName;
    public GameObject prefab;
    public GameObject ghostPrefab;
    public float floorDistance;
    public Sprite furnitureImage;
}   

public enum FurnitureType
{
    Shelf,
    Freezer,
    Counter
}