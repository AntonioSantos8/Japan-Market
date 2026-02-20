using System.Collections.Generic;
using UnityEngine;
public enum Furnitures
{
    Shelf,
    Freezer
}
public class FurnituresManager : MonoBehaviour
{
    public List<GameObject> furnitures = new List<GameObject>();
    [SerializeField] private Furnitures furnitures_enum;
    [SerializeField] private Furniture shelf, freezer;
    [SerializeField] private GameObject[] greenFurnitures;
    [SerializeField] private GameObject furnituresFather;
    private void Awake()
    {
        ServiceLocator.Register(this);
    }
    private void Update()
    {
        //SO PRA TESTE
        if (Input.GetKeyDown(KeyCode.E))
        {
            AddFurniture();
        }
        if(Input.GetKeyDown(KeyCode.T))
        {
            furnitures_enum = Furnitures.Shelf;
            greenFurnitures[0].SetActive(true);
            greenFurnitures[1].SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.Y))
        {
            furnitures_enum = Furnitures.Freezer;
            greenFurnitures[0].SetActive(false);
            greenFurnitures[1].SetActive(true);
        }
    }
    public void AddFurniture()
    {
        GameObject obj = null;

        switch (furnitures_enum)
        {
            case Furnitures.Shelf:
                obj = Instantiate(shelf.prefab, greenFurnitures[0].transform.position, greenFurnitures[0].transform.rotation);
                break;

            case Furnitures.Freezer:
                obj = Instantiate(freezer.prefab, greenFurnitures[1].transform.position, greenFurnitures[1].transform.rotation);
                break;
        }

        obj.transform.SetParent(furnituresFather.transform);
        furnitures.Add(obj);
    }
}
