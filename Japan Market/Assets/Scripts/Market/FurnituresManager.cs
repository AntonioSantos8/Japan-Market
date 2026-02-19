using System.Collections.Generic;
using UnityEngine;
public enum Furnitures
{
    Shelf,
    Freezer
}
public class FurnituresManager : MonoBehaviour
{
    public List<Transform> furnitures = new List<Transform>();
    private Furnitures furnitures_enum;
    [SerializeField] private Furniture shelf, freezer;
    [SerializeField] private Transform[] greenFurnitures;
    private void Awake()
    {
        ServiceLocator.Register(this);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            AddFurniture();
        }
    }
    public void AddFurniture()
    {
        switch (furnitures_enum)
        {
            case Furnitures.Shelf:
                Instantiate(shelf.prefab, greenFurnitures[0]);
                break; 
            case Furnitures.Freezer:
                Instantiate(freezer.prefab, greenFurnitures[1]);
                break;
        }
    }
}
