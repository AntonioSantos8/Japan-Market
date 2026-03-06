using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] Items myType;
    bool past = false;

    public Items GetItemType() => myType;

    public bool PassedItem()
    {
        return past;
    }

    public void MarkAsPast()
    {
        past = true;
    }
}