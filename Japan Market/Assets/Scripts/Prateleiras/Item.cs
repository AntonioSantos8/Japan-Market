using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField]Items myType;
    public Items GetItemType() => myType;
}
