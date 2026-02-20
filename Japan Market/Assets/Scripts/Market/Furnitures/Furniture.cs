using UnityEngine;

[CreateAssetMenu(fileName = "Furniture", menuName = "Scriptable Objects/Furniture")]
public class Furniture : ScriptableObject
{
    public string name_;
    public GameObject prefab;
}
