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
    [SerializeField] private GameObject furnituresFather;
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
        GameObject obj = null;

        switch (furnitures_enum)
        {
            case Furnitures.Shelf:
                obj = Instantiate(shelf.prefab, greenFurnitures[0].position, greenFurnitures[0].rotation);
                break;

            case Furnitures.Freezer:
                obj = Instantiate(freezer.prefab, greenFurnitures[1].position, greenFurnitures[1].rotation);
                break;
        }

        if (obj != null)
        {
            obj.transform.SetParent(furnituresFather.transform);
            obj.transform.localScale = Vector3.one;
        }
    }
}
