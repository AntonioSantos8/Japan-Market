using UnityEngine;

[CreateAssetMenu(fileName = "Trash", menuName = "Scriptable Objects/TrashData")]
public class TrashData : ScriptableObject
{
    public TrashType type;
    public string trashName;
    public GameObject prefab;
}

public enum TrashType
{
    Plastic,
    Metal,
    Paper,
    Organic
}