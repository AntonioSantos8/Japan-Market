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
    [SerializeField] private GameObject[] furnituresTranslucent;
    [SerializeField] private GameObject furnituresFather;

    public bool furnitureInWay;
    private bool canAddFurniture;
    private void Awake()
    {
        ServiceLocator.Register(this);
    }
    private void Update()
    {
        if (!furnitureInWay)
        {
            canAddFurniture = true;
        }
        else
            canAddFurniture = false;

        if (Input.GetKeyDown(KeyCode.E))
        {
            AddFurniture();
        }
        if(Input.GetKeyDown(KeyCode.T))
        {
            furnitures_enum = Furnitures.Shelf;
            furnituresTranslucent[0].SetActive(true);
            furnituresTranslucent[1].SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.Y))
        {
            furnitures_enum = Furnitures.Freezer;
            furnituresTranslucent[0].SetActive(false);
            furnituresTranslucent[1].SetActive(true);
        }
    }
    public void AddFurniture()
    {
        if (!canAddFurniture) return;
        GameObject obj = null;
        switch (furnitures_enum)
        {
            case Furnitures.Shelf:
                obj = Instantiate(shelf.prefab, furnituresTranslucent[0].transform.position, furnituresTranslucent[0].transform.rotation);
                break;

            case Furnitures.Freezer:
                obj = Instantiate(freezer.prefab, furnituresTranslucent[1].transform.position, furnituresTranslucent[1].transform.rotation);
                break;
        }

        obj.transform.SetParent(furnituresFather.transform);
        furnitures.Add(obj);
    }
}
